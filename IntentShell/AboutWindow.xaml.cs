using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace EasyCommand;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var date = File.GetLastWriteTime(assembly.Location);
        var os = Environment.OSVersion;

        VersionText.Text = $"Version: {version}";
        BuildDateText.Text = $"Build date: {date}";
        OSText.Text = $"OS: {os}";
    }

    private void OkBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
