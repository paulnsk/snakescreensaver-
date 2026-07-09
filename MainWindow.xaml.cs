using System;
using System.Linq;
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
    private const double MouseExitThresholdSq = 9.0;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        _screensaverMode = Environment.GetCommandLineArgs()
            .Skip(1)
            .Any(a => string.Equals(a, "/s", StringComparison.OrdinalIgnoreCase));

        if (_screensaverMode) EnableScreensaverMode();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        HideFromAltTab();

        Focus();
        Keyboard.Focus(this);

        Game.Focus();
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
        MouseDown += (_, _) => CloseAndShutdown();
        MouseMove += Screensaver_MouseMove;

        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            _mouseAnchor = PointToScreen(Mouse.GetPosition(this));
            _mouseArmed = true;
            timer.Stop();
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
            CloseAndShutdown();
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
