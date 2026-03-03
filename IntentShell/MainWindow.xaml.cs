using OpenAI.Chat;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using IntentShell.Services;

namespace IntentShell;

public partial class MainWindow : Window
{
    private ChatClient? _chatClient;
    private AppConfig? _cfg;

    private string? _currentFilePath;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();

        // Prevent user interaction before init
        DraftBtn.IsEnabled = false;
        RunBtn.IsEnabled = false;

        ContentRendered += MainWindow_ContentRendered;
    }

    private void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        // Ensure it runs only once
        ContentRendered -= MainWindow_ContentRendered;

        try
        {
            SetStatus("Initializing...");

            _cfg = ConfigLoader.LoadAllowMissingKey(AppContext.BaseDirectory);

            // cfg.ApiKey should already include DPAPI store fallback if you wired ConfigLoader as discussed.
            // But to be ironclad even if cfg.ApiKey is empty, we still prompt here.
            string apiKey = _cfg.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = PromptForApiKeyOrExit();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    // user canceled; remain signed out
                    SetSignedOutState("API key not configured.");
                    return;
                }

                // DPAPI store (primary persistence)
                ApiKeyStore.Save(apiKey);

                // Reload config to keep model/default prompt consistent
                _cfg = ConfigLoader.LoadAllowMissingKey(AppContext.BaseDirectory);
            }

            _chatClient = new ChatClient(_cfg.Model, apiKey);

            PromptInput.Text = _cfg.DefaultPrompt;

            DraftBtn.IsEnabled = true;
            RunBtn.IsEnabled = !string.IsNullOrWhiteSpace(PlanBox.Text);

            SetStatus("Ready");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup failed:\n\n{ex}", "Startup Error",
                MessageBoxButton.OK, MessageBoxImage.Error);

            SetSignedOutState("Startup failed.");
        }
    }

    private string? PromptForApiKeyOrExit()
    {
        var dlg = new ApiKeyWindow { Owner = this };
        bool? ok = dlg.ShowDialog();

        if (ok != true || string.IsNullOrWhiteSpace(dlg.ApiKey))
            return null;

        return dlg.ApiKey.Trim();
    }

    private void SetSignedOutState(string status)
    {
        _chatClient = null;
        DraftBtn.IsEnabled = false;
        RunBtn.IsEnabled = false;
        SetStatus(status);
    }

    private void SetBusy(bool isBusy, string statusText)
    {
        _isBusy = isBusy;
        BusyBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;

        DraftBtn.IsEnabled = !isBusy && _chatClient != null;
        RunBtn.IsEnabled = !isBusy && _chatClient != null && !string.IsNullOrWhiteSpace(PlanBox.Text);

        SetStatus(statusText);
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private async void DraftBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_chatClient is null)
        {
            MessageBox.Show("API key not configured. Use File → Set API Key.", "Not Ready",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string originalPrompt = PromptInput.Text;
        if (string.IsNullOrWhiteSpace(originalPrompt))
        {
            MessageBox.Show("Please enter a prompt first.", "No Prompt",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetBusy(true, "Drafting command...");
            PlanBox.Text = "Generating...";

            bool usePowerShell = PowerShellChk.IsChecked == true;
            string shellType = usePowerShell ? "PowerShell" : "Windows CMD";

            string userPrompt =
                $"You generate Windows commands.\n" +
                $"Output MUST be a single runnable {shellType} command only.\n" +
                $"No explanations. No markdown. No extra text.\n" +
                $"Avoid destructive actions (delete/format/registry edits) unless explicitly requested.\n" +
                $"If you cannot produce a valid {shellType} command for the request, output exactly:\n" +
                $"Do not understand you.\n\n" +
                $"User request:\n{originalPrompt}";

            ChatCompletion completion = await _chatClient.CompleteChatAsync(userPrompt);
            string response = completion.Content[0].Text.Trim();

            if (response.Equals("Do not understand you.", StringComparison.OrdinalIgnoreCase))
            {
                PlanBox.Text = string.Empty;
                MessageBox.Show(
                    "I couldn't generate a safe, valid command for that request. Try rephrasing with more specifics.",
                    "Command Generation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetStatus("Ready");
                return;
            }

            PlanBox.Text = response;
            RunBtn.IsEnabled = true;
            SetStatus("Draft ready");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error calling ChatGPT: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            PlanBox.Text = string.Empty;
            SetStatus("Ready");
        }
        finally
        {
            SetBusy(false, string.IsNullOrWhiteSpace(PlanBox.Text) ? "Ready" : "Draft ready");
        }
    }

    private async void RunBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_chatClient is null)
        {
            MessageBox.Show("API key not configured. Use File → Set API Key.", "Not Ready",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string command = PlanBox.Text;
        if (string.IsNullOrWhiteSpace(command))
        {
            MessageBox.Show("No command to run. Please draft a command first.", "No Command",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool usePowerShell = PowerShellChk.IsChecked == true;

        if (CommandSafety.LooksPotentiallyDestructive(command, usePowerShell))
        {
            var confirm = MessageBox.Show(
                CommandSafety.GetConfirmationText(),
                "Confirm execution",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                SetStatus("Canceled");
                return;
            }
        }

        try
        {
            SetBusy(true, "Running...");
            if (usePowerShell)
            {
                string encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
                await ExecuteProcessAsync("powershell.exe", "-NoProfile -NonInteractive -EncodedCommand " + encodedCommand);
            }
            else
            {
                await ExecuteProcessAsync("cmd.exe", "/c " + command);
            }
        }
        finally
        {
            SetBusy(false, "Ready");
        }
    }

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        PromptInput.Clear();
        PlanBox.Clear();
        OutputBox.Clear();
        PromptInput.Focus();
        _currentFilePath = null;
        SetStatus("Ready");
    }

    private async Task ExecuteProcessAsync(string fileName, string arguments)
    {
        OutputBox.Text = "Executing...";

        try
        {
            await Task.Run(() =>
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = AppContext.BaseDirectory,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                using var process = new System.Diagnostics.Process();
                process.StartInfo = processInfo;

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                using var outputWaitHandle = new System.Threading.ManualResetEvent(false);
                using var errorWaitHandle = new System.Threading.ManualResetEvent(false);

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                    {
                        try { outputWaitHandle.Set(); } catch { }
                    }
                    else outputBuilder.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                    {
                        try { errorWaitHandle.Set(); } catch { }
                    }
                    else errorBuilder.AppendLine(e.Data);
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();

                    outputWaitHandle.WaitOne(3000);
                    errorWaitHandle.WaitOne(3000);
                }
                catch (Exception ex)
                {
                    errorBuilder.AppendLine("Process execution error: " + ex.Message);
                }

                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();

                if (!string.IsNullOrEmpty(error) && error.Contains("#< CLIXML") && !string.IsNullOrWhiteSpace(output))
                    error = string.Empty;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(error))
                        OutputBox.Text = output + "\nError:\n" + error;
                    else
                        OutputBox.Text = string.IsNullOrWhiteSpace(output)
                            ? "(Command executed successfully but produced no output)"
                            : output;
                });
            });
        }
        catch (Exception ex)
        {
            OutputBox.Text = $"Error executing command: {ex.Message}";
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveAsBtn_Click(sender, e);
        }
        else
        {
            try
            {
                File.WriteAllText(_currentFilePath, PlanBox.Text);
                MessageBox.Show("Saved successfully.", "Save",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveAsBtn_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog();

        bool isPowerShell = PowerShellChk.IsChecked == true;
        if (isPowerShell)
        {
            saveFileDialog.Filter = "PowerShell Script (*.ps1)|*.ps1|All files (*.*)|*.*";
            saveFileDialog.DefaultExt = ".ps1";
            saveFileDialog.FileName = "script.ps1";
        }
        else
        {
            saveFileDialog.Filter = "Batch File (*.bat)|*.bat|All files (*.*)|*.*";
            saveFileDialog.DefaultExt = ".bat";
            saveFileDialog.FileName = "script.bat";
        }

        if (saveFileDialog.ShowDialog() == true)
        {
            _currentFilePath = saveFileDialog.FileName;
            try
            {
                File.WriteAllText(_currentFilePath, PlanBox.Text);
                MessageBox.Show("Saved successfully.", "Save As",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExitBtn_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void AboutBtn_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void ShortcutsBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Keyboard shortcuts:\n\n" +
            "• Ctrl+Enter  Draft\n" +
            "• F5         Run\n" +
            "• Ctrl+S     Save script\n" +
            "• Ctrl+L     Clear\n",
            "Keyboard Shortcuts",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void PlanBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isBusy) return;
        RunBtn.IsEnabled = _chatClient != null && !string.IsNullOrWhiteSpace(PlanBox.Text);
    }

    private void PowerShellChk_Changed(object sender, RoutedEventArgs e)
    {
        _currentFilePath = null;
    }

    private void CopyScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(PlanBox.Text))
        {
            Clipboard.SetText(PlanBox.Text);
            SetStatus("Script copied");
        }
    }

    private void CopyOutputBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OutputBox.Text))
        {
            Clipboard.SetText(OutputBox.Text);
            SetStatus("Output copied");
        }
    }

    private void ClearOutputBtn_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Clear();
        SetStatus("Ready");
    }

    private void CopyPromptBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            Clipboard.SetText(PromptInput.Text);
            SetStatus("Prompt copied");
        }
    }

    private void PasteExampleBtn_Click(object sender, RoutedEventArgs e)
    {
        PromptInput.Text = "List directories in C:\\";
        PromptInput.Focus();
        PromptInput.CaretIndex = PromptInput.Text.Length;
        SetStatus("Example pasted");
    }

    private void PromptInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) ==
            System.Windows.Input.ModifierKeys.Control)
        {
            e.Handled = true;
            DraftBtn_Click(this, new RoutedEventArgs());
            return;
        }

        if (e.Key == System.Windows.Input.Key.F5)
        {
            e.Handled = true;
            if (RunBtn.IsEnabled)
                RunBtn_Click(this, new RoutedEventArgs());
            return;
        }

        if (e.Key == System.Windows.Input.Key.L &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) ==
            System.Windows.Input.ModifierKeys.Control)
        {
            e.Handled = true;
            ClearBtn_Click(this, new RoutedEventArgs());
        }
    }

    // --- API Key Management (File menu) ---

    private void ClearApiKey_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will remove the stored API key for this Windows user.\n\nContinue?",
            "Clear API Key",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            ApiKeyStore.Clear();
            _chatClient = null;

            DraftBtn.IsEnabled = false;
            RunBtn.IsEnabled = false;

            SetStatus("API key cleared.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to clear API key:\n\n{ex}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetApiKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var apiKey = PromptForApiKeyOrExit();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                SetStatus("API key not changed.");
                return;
            }

            ApiKeyStore.Save(apiKey);

            _cfg ??= ConfigLoader.LoadAllowMissingKey(AppContext.BaseDirectory);
            _chatClient = new ChatClient(_cfg.Model, apiKey);

            DraftBtn.IsEnabled = true;
            RunBtn.IsEnabled = !string.IsNullOrWhiteSpace(PlanBox.Text);

            SetStatus("API key updated.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to set API key:\n\n{ex}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}