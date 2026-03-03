using System;
using System.Windows;

namespace EasyCommand;

public partial class ApiKeyWindow : Window
{
    public string ApiKey { get; private set; } = string.Empty;

    public ApiKeyWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => KeyBox.Focus();
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        string key = GetCurrentKey().Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Please enter an API key.", "Missing key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ApiKey = key;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowKeyChk_Checked(object sender, RoutedEventArgs e)
    {
        KeyTextBox.Text = KeyBox.Password;
        KeyTextBox.Visibility = Visibility.Visible;
        KeyBox.Visibility = Visibility.Collapsed;
        KeyTextBox.Focus();
        KeyTextBox.CaretIndex = KeyTextBox.Text.Length;
    }

    private void ShowKeyChk_Unchecked(object sender, RoutedEventArgs e)
    {
        KeyBox.Password = KeyTextBox.Text;
        KeyBox.Visibility = Visibility.Visible;
        KeyTextBox.Visibility = Visibility.Collapsed;
        KeyBox.Focus();
    }

    private string GetCurrentKey()
    {
        return ShowKeyChk.IsChecked == true ? (KeyTextBox.Text ?? string.Empty) : (KeyBox.Password ?? string.Empty);
    }
}
