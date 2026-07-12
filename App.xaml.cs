using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SnakeScreensaver;

public partial class App : Application
{
    public static TrayHost Tray { get; private set; } = null!;

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        Log.Line("== App.OnStartup");

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
        Current.DispatcherUnhandledException += OnDispatcherUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Tray = new TrayHost();
        Tray.Start();
    }

    private void App_OnExit(object sender, ExitEventArgs e)
    {
        Log.Line("== App.OnExit");
        Tray?.Dispose();
    }

    private static void OnDomainUnhandled(object? s, UnhandledExceptionEventArgs ea)
    {
        Log.Line($"AppDomain.UnhandledException (terminating={ea.IsTerminating})");
        Log.Exc("AppDomain.UnhandledException", ea.ExceptionObject as System.Exception);
    }

    private static void OnDispatcherUnhandled(object? s, DispatcherUnhandledExceptionEventArgs ea)
    {
        Log.Exc("Dispatcher.UnhandledException", ea.Exception);
    }

    private static void OnUnobservedTask(object? s, UnobservedTaskExceptionEventArgs ea)
    {
        Log.Exc("TaskScheduler.Unobserved", ea.Exception);
    }
}

