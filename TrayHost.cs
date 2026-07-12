using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace SnakeScreensaver;

public sealed class TrayHost : IDisposable
{
    private NotifyIcon? _icon;
    private MainWindow? _window;
    private TrayMenuWindow? _menu;
    private SettingsWindow? _settings;
    private Thread? _idleThread;
    private volatile bool _running;

    public void Start()
    {
        Log.Line($"TrayHost.Start on thread {Thread.CurrentThread.ManagedThreadId}");
        _icon = new NotifyIcon
        {
            Icon = BuildIcon(),
            Text = "SnakeScreensaver",
            Visible = true
        };
        _icon.MouseClick += OnIconClick;
        _icon.MouseDoubleClick += (_, ma) =>
        {
            if (ma.Button == MouseButtons.Left) LaunchSnake();
        };

        _running = true;
        _idleThread = new Thread(IdleLoop) { IsBackground = true, Name = "idle-monitor" };
        _idleThread.Start();
    }

    private void OnIconClick(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            ShowMenu();
    }

    private void ShowMenu()
    {
        Log.Line("ShowMenu: enter");
        if (_menu != null && _menu.IsVisible) { _menu.Activate(); Log.Line("ShowMenu: reused existing"); return; }
        _menu = new TrayMenuWindow(this);
        _menu.Deactivated += (_, _) =>
        {
            try { _menu.Hide(); }
            catch (Exception ex) { Log.Exc("ShowMenu.Deactivated.Hide", ex); }
        };

        var size = MeasureWindow(_menu);
        var wa = SystemParameters.WorkArea;
        var cursor = System.Windows.Forms.Cursor.Position;

        double right = Math.Min(cursor.X + 8, wa.Right - 2);
        double bottom = wa.Bottom;

        _menu.InitAnchor(right, bottom, size);

        _menu.Left = -10000;
        _menu.Top = -10000;
        _menu.Show();

        var loc = _menu.ComputeRestLocation();
        _menu.Left = loc.X;
        _menu.Top = loc.Y;
        _menu.Activate();
        Log.Line($"ShowMenu: opened at ({loc.X}, {loc.Y}) size {size.Width}x{size.Height} anchor right={right} bottom={bottom} workArea={wa}");
    }

    private static System.Windows.Size MeasureWindow(TrayMenuWindow m)
    {
        m.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        m.Arrange(new Rect(new System.Windows.Point(-99999, -99999), m.DesiredSize));
        var sz = new System.Windows.Size(m.ActualWidth, m.ActualHeight);
        if (sz.Width <= 0 || sz.Height <= 0) sz = m.DesiredSize;
        if (sz.Width <= 0) sz = new System.Windows.Size(360, sz.Height);
        if (sz.Height <= 0) sz = new System.Windows.Size(sz.Width, 480);
        return sz;
    }

    public void LaunchSnake()
    {
        if (_window != null && _window.IsVisible)
        {
            _window.Activate();
            return;
        }
        if (_window != null)
        {
            _window.Close();
            _window = null;
        }
        _window = new MainWindow();
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    public void HideSnake()
    {
        _window?.Close();
        _window = null;
    }

    public void OpenSettings()
    {
        Log.Line("OpenSettings: enter");
        try
        {
            if (_settings != null && _settings.IsVisible) { _settings.Activate(); Log.Line("OpenSettings: reuse"); return; }
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => _settings = null;
            _settings.Show();
            Log.Line("OpenSettings: shown");
        }
        catch (Exception ex) { Log.Exc("OpenSettings threw", ex); throw; }
    }

    public void Quit()
    {
        var wState = _window == null ? "null" : (_window.IsVisible ? "visible" : "hidden");
        var sState = _settings == null ? "null" : (_settings.IsVisible ? "visible" : "hidden");
        var mState = _menu == null ? "null" : (_menu.IsVisible ? "visible" : "hidden");
        Log.Line($"Quit: enter (window:{wState} settings:{sState} menu:{mState} icon:{(_icon != null ? "exists" : "null")})");
        _running = false;
        try { _window?.Close(); Log.Line("Quit: snake window closed"); }
        catch (Exception ex) { Log.Exc("Quit.Close(window)", ex); }
        try { _settings?.Close(); Log.Line("Quit: settings closed"); }
        catch (Exception ex) { Log.Exc("Quit.Close(settings)", ex); }
        try { _menu?.Hide(); Log.Line("Quit: menu hidden"); }
        catch (Exception ex) { Log.Exc("Quit.Hide(menu)", ex); }
        if (_icon != null) _icon.Visible = false;
        Log.Line("Quit: icon invisible, scheduling Shutdown");
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                Log.Line("Quit.Scheduled: calling Shutdown");
                System.Windows.Application.Current.Shutdown();
                Log.Line("Quit.Scheduled: Shutdown returned");
            }
            catch (Exception ex) { Log.Exc("Quit.Scheduled.Shutdown threw", ex); throw; }
        }));
    }

    private void IdleLoop()
    {
        const double pollSec = 1.0;
        while (_running)
        {
            Thread.Sleep(TimeSpan.FromSeconds(pollSec));
            try
            {
                if (Settings.IdleMinutes <= 0) continue;
                if (_window != null && _window.IsVisible) continue;
                var idle = IdleMonitor.GetIdleSeconds();
                if (idle >= Settings.IdleMinutes * 60.0)
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(LaunchSnake);
            }
            catch { }
        }
    }

    private static Icon BuildIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);
        var cx = 16; var cy = 16;
        var r = 8;
        for (int i = 7; i >= 0; i--)
        {
            var hue = (i * 45.0) % 360.0;
            var color = HslToColor(hue, 1.0, 0.55);
            using var br = new SolidBrush(color);
            var a = i * 0.4;
            var x = cx + Math.Cos(a) * r;
            var y = cy + Math.Sin(a) * r;
            g.FillEllipse(br, (float)(x - r / 2.5), (float)(y - r / 2.5), (float)(r / 1.25), (float)(r / 1.25));
        }
        using var head = new SolidBrush(System.Drawing.Color.White);
        g.FillEllipse(head, cx - 3, cy - 3, 6, 6);
        using var eye = new SolidBrush(System.Drawing.Color.Black);
        g.FillEllipse(eye, cx - 1, cy - 1, 2, 2);
        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    private static System.Drawing.Color HslToColor(double h, double s, double l)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var hp = h / 60.0;
        var x = c * (1 - Math.Abs((hp % 2) - 1));
        double r1, g1, b1;
        if (hp < 1) { r1 = c; g1 = x; b1 = 0; }
        else if (hp < 2) { r1 = x; g1 = c; b1 = 0; }
        else if (hp < 3) { r1 = 0; g1 = c; b1 = x; }
        else if (hp < 4) { r1 = 0; g1 = x; b1 = c; }
        else if (hp < 5) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }
        var m = l - c / 2;
        return System.Drawing.Color.FromArgb(
            (int)Math.Round((r1 + m) * 255),
            (int)Math.Round((g1 + m) * 255),
            (int)Math.Round((b1 + m) * 255));
    }

    public void Dispose()
    {
        _running = false;
        if (_icon != null) { _icon.Visible = false; _icon.Dispose(); _icon = null; }
    }
}
