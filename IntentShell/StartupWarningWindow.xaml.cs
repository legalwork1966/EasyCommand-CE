using System.Windows;

namespace IntentShell
{
    public partial class StartupWarningWindow : Window
    {
        public StartupWarningWindow()
        {
            InitializeComponent();
        }

        private void AgreeCheck_Changed(object sender, RoutedEventArgs e)
        {
            ContinueBtn.IsEnabled = AgreeCheck.IsChecked == true;
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}