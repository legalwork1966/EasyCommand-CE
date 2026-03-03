using OpenAI.Chat;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using EasyCommand.Services;

namespace EasyCommand;

public partial class MainWindow : Window
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private ChatClient?          _chatClient;
    private AppConfig?           _cfg;
    private string?              _currentFilePath;
    private bool                 _isBusy;
    private CancellationTokenSource? _cts;

    private const string UnknownSentinel = "Do not understand you.";

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();

        DraftBtn.IsEnabled = false;
        RunBtn.IsEnabled   = false;

        ContentRendered += MainWindow_ContentRendered;
    }

    // ── Init ──────────────────────────────────────────────────────────────────


private bool CheckEula()
{
    string appData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EasyCommand");

    Directory.CreateDirectory(appData);

    string acceptedFlag = Path.Combine(appData, "eula.accepted");
    if (File.Exists(acceptedFlag))
        return true;

    // Load EULA text from install directory (same folder as EasyCommand.exe)
    string eulaPath = Path.Combine(AppContext.BaseDirectory, "EULA.txt");
    string eulaText =
        File.Exists(eulaPath)
            ? File.ReadAllText(eulaPath)
            : "EULA.txt was not found. Please reinstall the application.";

    var dlg = new IntentShell.EulaWindow(eulaText, this);
    bool? accepted = dlg.ShowDialog();

    if (accepted == true)
    {
        File.WriteAllText(acceptedFlag, "accepted");
        return true;
    }

    return false;
}
    private void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= MainWindow_ContentRendered;
        if (!CheckEula())
        {
            Application.Current.Shutdown();
            return;
        }

        try
        {
            SetStatus("Initializing…");

            _cfg = ConfigLoader.LoadAllowMissingKey(AppContext.BaseDirectory);
            string? apiKey = _cfg.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = PromptForApiKeyOrExit();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    SetSignedOutState("API key not configured.");
                    return;
                }

                ApiKeyStore.Save(apiKey);
                _cfg = ConfigLoader.LoadAllowMissingKey(AppContext.BaseDirectory);
            }

            _chatClient = new ChatClient(_cfg.Model, apiKey);

            PromptInput.Text = _cfg.DefaultPrompt;
            PromptInput.Focus();
            PromptInput.SelectAll();
            DraftBtn.IsEnabled = true;
            RunBtn.IsEnabled   = !string.IsNullOrWhiteSpace(PlanBox.Text);

            UpdateShellLabel();
            SetStatus("Ready");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup failed:\n\n{ex}", "Startup Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetSignedOutState("Startup failed.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? PromptForApiKeyOrExit()
    {
        var dlg = new ApiKeyWindow { Owner = this };
        return dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.ApiKey)
            ? dlg.ApiKey.Trim()
            : null;
    }

    private void SetSignedOutState(string status)
    {
        _chatClient = null;
        DraftBtn.IsEnabled = false;
        RunBtn.IsEnabled   = false;
        SetStatus(status);
    }

    private void SetBusy(bool isBusy, string statusText)
    {
        _isBusy = isBusy;

        BusyBar.Visibility    = isBusy ? Visibility.Visible    : Visibility.Collapsed;
        CancelBtn.Visibility  = isBusy ? Visibility.Visible    : Visibility.Collapsed;
        DraftBtn.Visibility   = isBusy ? Visibility.Collapsed  : Visibility.Visible;
        RunBtn.Visibility     = isBusy ? Visibility.Collapsed  : Visibility.Visible;

        if (!isBusy)
        {
            DraftBtn.IsEnabled = _chatClient != null;
            RunBtn.IsEnabled   = _chatClient != null && !string.IsNullOrWhiteSpace(PlanBox.Text);
        }

        SetStatus(statusText);
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private bool UsePowerShell => PowerShellRdo.IsChecked == true;

    private void UpdateShellLabel()
    {
        ShellModeLabel.Text = UsePowerShell ? "PowerShell" : "CMD";
    }

    private void UpdateWindowTitle()
    {
        Title = string.IsNullOrEmpty(_currentFilePath)
            ? "Easy Command"
            : $"Easy Command — {Path.GetFileName(_currentFilePath)}";
    }

    private void ShowExitCode(int code)
    {
        ExitCodeLabel.Visibility = Visibility.Visible;
        ExitCodeText.Visibility  = Visibility.Visible;
        ExitCodeText.Text        = code.ToString();
        ExitCodeText.Foreground  = code == 0
            ? new SolidColorBrush(Color.FromRgb(14, 122, 60))
            : new SolidColorBrush(Color.FromRgb(192, 57, 43));
    }

    private void SetOutputBadge(bool success)
    {
        OutputBadge.Visibility = Visibility.Visible;
        if (success)
        {
            OutputBadge.Background  = new SolidColorBrush(Color.FromRgb(235, 250, 242));
            OutputBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(130, 210, 170));
            OutputBadgeText.Text       = "✓ Success";
            OutputBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(14, 122, 60));
        }
        else
        {
            OutputBadge.Background  = new SolidColorBrush(Color.FromRgb(253, 237, 236));
            OutputBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(240, 160, 155));
            OutputBadgeText.Text       = "✗ Error";
            OutputBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43));
        }
    }

    private void UpdateScriptBadge()
    {
        if (PlanBox is null) return;

        if (string.IsNullOrWhiteSpace(PlanBox.Text))
        {
            ScriptBadge.Visibility = Visibility.Collapsed;
        }
        else
        {
            ScriptBadge.Visibility = Visibility.Visible;
            ScriptBadgeText.Text   = UsePowerShell ? "ps1" : "bat";
        }
    }

    // ── Draft ─────────────────────────────────────────────────────────────────

    private async void DraftBtn_Click(object sender, RoutedEventArgs e)
    {
        await DraftCommandAsync();
    }

    private async Task DraftCommandAsync()
    {
        if (_chatClient is null)
        {
            MessageBox.Show("API key not configured. Use File → Set API Key.", "Not Ready",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string originalPrompt = PromptInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(originalPrompt))
        {
            MessageBox.Show("Please enter a prompt first.", "No Prompt",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cts = new CancellationTokenSource();

        try
        {
            SetBusy(true, "Drafting command…");
            PlanBox.Text = "Generating…";

            string shellType  = UsePowerShell ? "PowerShell" : "Windows CMD";
            string userPrompt =
                $"You generate Windows commands.\n"                                                        +
                $"Output MUST be a single runnable {shellType} command only.\n"                            +
                $"No explanations. No markdown. No extra text.\n"                                          +
                $"Avoid destructive actions (delete/format/registry edits) unless explicitly requested.\n" +
                $"If you cannot produce a valid {shellType} command, output exactly:\n"                    +
                $"{UnknownSentinel}\n\n"                                                                   +
                $"User request:\n{originalPrompt}";

            ChatCompletion completion = await _chatClient.CompleteChatAsync(
                [new UserChatMessage(userPrompt)], cancellationToken: _cts.Token);

            string response = completion.Content[0].Text.Trim();

            if (response.Equals(UnknownSentinel, StringComparison.OrdinalIgnoreCase))
            {
                PlanBox.Text = string.Empty;
                MessageBox.Show(
                    "I couldn't generate a safe, valid command for that request.\nTry rephrasing with more specifics.",
                    "Command Generation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                SetStatus("Ready");
                return;
            }

            PlanBox.Text = response;
            UpdateScriptBadge();
            SetStatus("Draft ready — review and click Run");
        }
        catch (OperationCanceledException)
        {
            PlanBox.Text = string.Empty;
            SetStatus("Canceled");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error calling API:\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            PlanBox.Text = string.Empty;
            SetStatus("Ready");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetBusy(false, string.IsNullOrWhiteSpace(PlanBox.Text) ? "Ready" : "Draft ready");
        }
    }

    // ── Run ───────────────────────────────────────────────────────────────────

    private async void RunBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_chatClient is null)
        {
            MessageBox.Show("API key not configured. Use File → Set API Key.", "Not Ready",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string command = PlanBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(command))
        {
            MessageBox.Show("No command to run. Please draft a command first.", "No Command",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CommandSafety.LooksPotentiallyDestructive(command, UsePowerShell))
        {
            var confirm = MessageBox.Show(
                CommandSafety.GetConfirmationText(),
                "Confirm Execution",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                SetStatus("Canceled");
                return;
            }
        }

        _cts = new CancellationTokenSource();
        OutputBadge.Visibility = Visibility.Collapsed;
        ExitCodeLabel.Visibility = ExitCodeText.Visibility = Visibility.Collapsed;

        try
        {
            SetBusy(true, "Running…");

            if (UsePowerShell)
            {
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
                await ExecuteProcessAsync("powershell.exe",
                    "-NoProfile -NonInteractive -EncodedCommand " + encoded, _cts.Token);
            }
            else
            {
                await ExecuteProcessAsync("cmd.exe", "/c " + command, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            OutputBox.Text = "(Execution canceled)";
            SetStatus("Canceled");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetBusy(false, "Ready");
        }
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        SetStatus("Canceling…");
    }

    // ── Process execution ─────────────────────────────────────────────────────

    private async Task ExecuteProcessAsync(string fileName, string arguments,
        CancellationToken ct = default)
    {
        OutputBox.Text = "Executing…";

        try
        {
            await Task.Run(() =>
            {
                var psi = new System.Diagnostics.ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput  = true,
                    RedirectStandardError   = true,
                    UseShellExecute         = false,
                    CreateNoWindow          = true,
                    WorkingDirectory        = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    StandardOutputEncoding  = Encoding.UTF8,
                    StandardErrorEncoding   = Encoding.UTF8
                };

                using var process = new System.Diagnostics.Process();
                process.StartInfo = psi;

                var outBuf = new StringBuilder();
                var errBuf = new StringBuilder();

                using var outDone = new ManualResetEventSlim(false);
                using var errDone = new ManualResetEventSlim(false);

                process.OutputDataReceived += (_, ev) =>
                {
                    if (ev.Data is null) outDone.Set();
                    else outBuf.AppendLine(ev.Data);
                };
                process.ErrorDataReceived += (_, ev) =>
                {
                    if (ev.Data is null) errDone.Set();
                    else errBuf.AppendLine(ev.Data);
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Poll for cancellation while waiting
                    while (!process.WaitForExit(200))
                    {
                        if (ct.IsCancellationRequested)
                        {
                            try { process.Kill(entireProcessTree: true); } catch { }
                            ct.ThrowIfCancellationRequested();
                        }
                    }

                    outDone.Wait(3000);
                    errDone.Wait(3000);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    errBuf.AppendLine("Process execution error: " + ex.Message);
                }

                string output = outBuf.ToString();
                string error  = errBuf.ToString();
                int    exitCode = process.ExitCode;

                // Suppress noisy CLIXML stream from PowerShell when stdout is present
                if (!string.IsNullOrEmpty(error)
                    && error.Contains("#< CLIXML")
                    && !string.IsNullOrWhiteSpace(output))
                    error = string.Empty;

                bool success = exitCode == 0 && string.IsNullOrWhiteSpace(error);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    OutputBox.Text = !string.IsNullOrEmpty(error)
                        ? output + "\n── stderr ──\n" + error
                        : string.IsNullOrWhiteSpace(output)
                            ? "(Command executed successfully with no output)"
                            : output;

                    ShowExitCode(exitCode);
                    SetOutputBadge(success);
                    SetStatus(success ? "Done" : "Done (with errors)");
                });
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            OutputBox.Text = $"Error executing command: {ex.Message}";
            SetOutputBadge(false);
        }
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        PromptInput.Clear();
        PlanBox.Clear();
        OutputBox.Clear();
        PromptInput.Focus();
        _currentFilePath = null;
        OutputBadge.Visibility = ScriptBadge.Visibility = Visibility.Collapsed;
        ExitCodeLabel.Visibility = ExitCodeText.Visibility = Visibility.Collapsed;
        UpdateWindowTitle();
        SetStatus("Ready");
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveAsBtn_Click(sender, e);
            return;
        }

        try
        {
            File.WriteAllText(_currentFilePath, PlanBox.Text);
            SetStatus($"Saved → {Path.GetFileName(_currentFilePath)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file:\n{ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAsBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog();

        if (UsePowerShell)
        {
            dlg.Filter      = "PowerShell Script (*.ps1)|*.ps1|All files (*.*)|*.*";
            dlg.DefaultExt  = ".ps1";
            dlg.FileName    = "script.ps1";
        }
        else
        {
            dlg.Filter      = "Batch File (*.bat)|*.bat|All files (*.*)|*.*";
            dlg.DefaultExt  = ".bat";
            dlg.FileName    = "script.bat";
        }

        if (dlg.ShowDialog() != true) return;

        _currentFilePath = dlg.FileName;

        try
        {
            File.WriteAllText(_currentFilePath, PlanBox.Text);
            UpdateWindowTitle();
            SetStatus($"Saved → {Path.GetFileName(_currentFilePath)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file:\n{ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Menu handlers ─────────────────────────────────────────────────────────

    private void ExitBtn_Click(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();

    private void AboutBtn_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void ShortcutsBtn_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Keyboard shortcuts:\n\n"  +
            "  Ctrl+Enter   Draft\n"   +
            "  F5           Run\n"     +
            "  Ctrl+S       Save script\n" +
            "  Ctrl+L       Clear all\n"   +
            "  Escape       Cancel busy operation\n" +
            "  F1           This dialog",
            "Keyboard Shortcuts",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ── API key management ────────────────────────────────────────────────────

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
            RunBtn.IsEnabled   = !string.IsNullOrWhiteSpace(PlanBox.Text);
            SetStatus("API key updated.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to set API key:\n\n{ex}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearApiKey_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will remove the stored API key for this Windows user.\n\nContinue?",
            "Clear API Key",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            ApiKeyStore.Clear();
            SetSignedOutState("API key cleared.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to clear API key:\n\n{ex}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Button helpers ────────────────────────────────────────────────────────

    private void CopyScriptBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(PlanBox.Text))
        {
            Clipboard.SetText(PlanBox.Text);
            SetStatus("Script copied to clipboard");
        }
    }

    private void CopyOutputBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OutputBox.Text))
        {
            Clipboard.SetText(OutputBox.Text);
            SetStatus("Output copied to clipboard");
        }
    }

    private void ClearOutputBtn_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Clear();
        OutputBadge.Visibility = Visibility.Collapsed;
        ExitCodeLabel.Visibility = ExitCodeText.Visibility = Visibility.Collapsed;
        SetStatus("Ready");
    }

    private void CopyPromptBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(PromptInput.Text))
        {
            Clipboard.SetText(PromptInput.Text);
            SetStatus("Prompt copied to clipboard");
        }
    }

    private void PasteExampleBtn_Click(object sender, RoutedEventArgs e)
    {
        PromptInput.Text = "List all running processes sorted by CPU usage";
        PromptInput.Focus();
        PromptInput.CaretIndex = PromptInput.Text.Length;
        SetStatus("Example pasted");
    }

    // ── Shell mode ────────────────────────────────────────────────────────────

    private async void ShellMode_Changed(object sender, RoutedEventArgs e)
    {
        _currentFilePath = null;
        UpdateWindowTitle();
        UpdateShellLabel();
        UpdateScriptBadge();

        if (!IsLoaded) return;
        if (string.IsNullOrWhiteSpace(PromptInput.Text)) return;

        string message = UsePowerShell
            ? "Do you want to redo the command in PowerShell?"
            : "Do you want to redo the script in Windows?";

        var result = MessageBox.Show(
            message,
            "Switch User Shell",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await DraftCommandAsync();
        }
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    /// <summary>Window-level hotkeys that work regardless of which control has focus.</summary>
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var mod = System.Windows.Input.Keyboard.Modifiers;
        bool ctrl = (mod & System.Windows.Input.ModifierKeys.Control) != 0;

        switch (e.Key)
        {
            case System.Windows.Input.Key.F1:
                e.Handled = true;
                ShortcutsBtn_Click(this, new RoutedEventArgs());
                break;

            case System.Windows.Input.Key.F5:
                e.Handled = true;
                if (RunBtn.IsEnabled) RunBtn_Click(this, new RoutedEventArgs());
                break;

            case System.Windows.Input.Key.Escape:
                if (_isBusy) { e.Handled = true; _cts?.Cancel(); }
                break;

            case System.Windows.Input.Key.S when ctrl:
                e.Handled = true;
                SaveBtn_Click(this, new RoutedEventArgs());
                break;

            case System.Windows.Input.Key.L when ctrl:
                e.Handled = true;
                ClearBtn_Click(this, new RoutedEventArgs());
                break;
        }
    }

    /// <summary>Ctrl+Enter in the prompt box triggers Draft.</summary>
    private void PromptInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            if (DraftBtn.IsEnabled) DraftBtn_Click(this, new RoutedEventArgs());
        }
    }

    // ── TextChanged ───────────────────────────────────────────────────────────

    private void PlanBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isBusy) return;
        RunBtn.IsEnabled = _chatClient != null && !string.IsNullOrWhiteSpace(PlanBox.Text);
        UpdateScriptBadge();
    }
}