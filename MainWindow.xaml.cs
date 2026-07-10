using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SnakeScreensaver;

public partial class MainWindow : Window
{
    private readonly bool _screensaverMode;
    private bool _mouseArmed;
    private Point _mouseAnchor;
    private const double MouseExitThresholdSq = 225.0;
    private static readonly TimeSpan MouseWarmup = TimeSpan.FromMilliseconds(1500);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        var args = Environment.GetCommandLineArgs();
        _screensaverMode = args.Length > 1 && MatchArg(args[1], "s");
        if (_screensaverMode)
        {
            DesktopHelper.Log("launch /s (MainWindow ctor)");
            EnableScreensaverMode();
        }
    }

    private static bool MatchArg(string a, string expected)
    {
        var s = a.TrimStart('/', '-').ToLowerInvariant();
        return s == expected;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        HideFromAltTab();
        DiagnoseDesktop();

        Focus();
        Keyboard.Focus(this);

        Game.Focus();
    }

    private void DiagnoseDesktop()
    {
        try
        {
            var sn = DesktopHelper.CurrentName();
            var taskbar = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
            var progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            var taskbarStr = taskbar != IntPtr.Zero ? $"0x{taskbar.ToInt64():X}" : "none";
            var progmanStr = progman != IntPtr.Zero ? $"0x{progman.ToInt64():X}" : "none";
            DesktopHelper.Log($"post-load desktop name='{sn}' taskbar={taskbarStr} progman={progmanStr}");
        }
        catch (Exception ex) { DesktopHelper.Log($"diag ex: {ex.Message}"); }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_screensaverMode || e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseAndShutdown();
        }
    }

    private void EnableScreensaverMode()
    {
        Cursor = Cursors.None;

        MouseDown += (_, _) => CloseAndShutdown();
        MouseMove += Screensaver_MouseMove;

        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = MouseWarmup };
        timer.Tick += (_, _) =>
        {
            try { _mouseAnchor = PointToScreen(Mouse.GetPosition(this)); }
            catch { _mouseAnchor = new Point(0, 0); }
            _mouseArmed = true;
            timer.Stop();
            DesktopHelper.Log("armed");
        };
        timer.Start();
    }

    private void Screensaver_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_mouseArmed) return;
        var sp = PointToScreen(e.GetPosition(this));
        var dx = sp.X - _mouseAnchor.X;
        var dy = sp.Y - _mouseAnchor.Y;
        if (dx * dx + dy * dy > MouseExitThresholdSq)
        {
            DesktopHelper.Log($"exit mouse move dx={dx:F1} dy={dy:F1}");
            CloseAndShutdown();
        }
    }

    private void CloseAndShutdown()
    {
        Close();
        Application.Current.Shutdown();
    }

    private void HideFromAltTab()
    {
        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        var hwnd = helper.Handle;
        var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, (int)(ex | WS_EX_TOOLWINDOW));
    }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    private static long GetWindowLong(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8) return (long)GetWindowLongPtr(hWnd, nIndex);
        return GetWindowLong32(hWnd, nIndex);
    }

    private static void SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr(hWnd, nIndex, new IntPtr(dwNewLong));
        else SetWindowLong32(hWnd, nIndex, dwNewLong);
    }
}
