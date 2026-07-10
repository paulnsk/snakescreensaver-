using System.Linq;
using System.Windows;

namespace SnakeScreensaver;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
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
        base.OnStartup(e);
    }
}
