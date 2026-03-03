using System.Windows;

namespace EasyCommand;

public partial class LicenseDialog : Window
{
    // Constructor matching MainWindow.xaml.cs usage
    public LicenseDialog(string eulaText, Window owner)
    {
        InitializeComponent();
        
        Owner = owner;
        EulaText.Text = eulaText;
    }

    // Default constructor for designer support
    public LicenseDialog()
    {
        InitializeComponent();
    }

    private void AgreeCheck_Checked(object sender, RoutedEventArgs e)
    {
        if (AcceptBtn != null)
        {
            AcceptBtn.IsEnabled = AgreeCheck.IsChecked == true;
        }
    }

    private void Decline_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
