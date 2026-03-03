using System.Windows;

namespace EasyCommand;

public partial class App : Application
{
    private static bool s_hasAcceptedResponsibility = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!s_hasAcceptedResponsibility)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            var dlg = new ResponsibilityDialog();
            bool? result = dlg.ShowDialog();

            if (result != true)
            {
                Shutdown();
                return;
            }

            s_hasAcceptedResponsibility = true;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
