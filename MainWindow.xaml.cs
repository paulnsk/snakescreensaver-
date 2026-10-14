using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SnakeScreensaver;

public partial class MainWindow : Window
{
    private bool _mouseArmed;
    private Point _mouseAnchor;
    private const double MouseExitThresholdSq = 256.0;
    private static readonly TimeSpan MouseWarmup = TimeSpan.FromMilliseconds(1500);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Cursor = Settings.Exit == Settings.ExitMode.MouseMove ? Cursors.None : Cursors.Arrow;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        HideFromAltTab();

        if (Settings.Render == Settings.Renderer.WpfTurbo)
            CreateTurboDisplays();

        Focus();
        Keyboard.Focus(this);

        Game.Focus();

        if (Settings.Exit == Settings.ExitMode.MouseMove)
        {
            MouseMove += MainWindow_MouseMove;
            MouseDown += (_, _) => Close();
            var t = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = MouseWarmup };
            t.Tick += (_, _) =>
            {
                try { _mouseAnchor = PointToScreen(Mouse.GetPosition(this)); }
                catch { _mouseAnchor = new Point(0, 0); }
                _mouseArmed = true;
                t.Stop();
            };
            t.Start();
        }
    }

    private void MainWindow_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_mouseArmed) return;
        var source = (Window)sender;
        var sp = source.PointToScreen(e.GetPosition(source));
        var dx = sp.X - _mouseAnchor.X;
        var dy = sp.Y - _mouseAnchor.Y;
        if (dx * dx + dy * dy > MouseExitThresholdSq)
            Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
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
