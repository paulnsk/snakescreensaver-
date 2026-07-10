using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SnakeScreensaver;

public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        if (e.Args.Length > 0)
        {
            var a = e.Args[0].TrimStart('/', '-').ToLowerInvariant();
            if (a == "p")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Shutdown(0);
                return;
            }
        }

        bool screensaver = e.Args.Length > 0
            && e.Args[0].TrimStart('/', '-').ToLowerInvariant() == "s";
        if (screensaver) DesktopHelper.Log("App.OnStartup /s: spawning worker thread");

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var worker = new Thread(() =>
        {
            try
            {
                if (screensaver) DesktopHelper.TrySwitchToDefault();

                var w = new MainWindow();
                w.Closed += (_, _) =>
                {
                    Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
                };
                w.Show();
                Dispatcher.Run();
            }
            catch (System.Exception ex) { DesktopHelper.Log($"worker ex: {ex.Message}"); }
        });
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }
}
