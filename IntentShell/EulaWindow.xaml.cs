using System.IO;
using System.Windows;

namespace IntentShell
{
    public partial class EulaWindow : Window
    {
        public EulaWindow(string eulaText, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            EulaText.Text = eulaText;
        }

        private void AgreeCheck_Checked(object sender, RoutedEventArgs e)
        {
            AcceptBtn.IsEnabled = AgreeCheck.IsChecked == true;
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}