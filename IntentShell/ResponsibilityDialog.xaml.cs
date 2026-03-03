using System.Windows;
using System.Windows.Controls;

namespace EasyCommand;

public partial class ResponsibilityDialog : Window
{
    public ResponsibilityDialog()
    {
        InitializeComponent();
        
        // Ensure buttons are in correct state initially
        UpdateControls();
    }

    private void AcceptCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateControls();
    }

    private void UpdateControls()
    {
        bool isChecked = AcceptCheckBox.IsChecked == true;
        if (ContinueButton != null)
        {
            ContinueButton.IsEnabled = isChecked;
        }
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
