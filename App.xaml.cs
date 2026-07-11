using System;
using System.Windows;

namespace SnakeScreensaver;

public partial class App : Application
{
    public static TrayHost Tray { get; private set; } = null!;

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Tray = new TrayHost();
        Tray.Start();
    }

    private void App_OnExit(object sender, ExitEventArgs e)
    {
        Tray?.Dispose();
    }
}
