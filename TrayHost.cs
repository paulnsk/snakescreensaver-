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
        if (_menu != null && _menu.IsVisible) { _menu.Activate(); return; }
        _menu = new TrayMenuWindow(this);
        _menu.Deactivated += (_, _) => _menu.Hide();
        var loc = ComputeMenuLocation(_menu);
        _menu.Left = loc.X;
        _menu.Top = loc.Y;
        _menu.Show();
        _menu.Activate();
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

    private static System.Windows.Point ComputeMenuLocation(TrayMenuWindow m)
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var size = MeasureWindow(m);
        var wa = SystemParameters.WorkArea;

        double sx = cursor.X - size.Width - 6;
        double sy = cursor.Y - size.Height - 6;

        if (sy < wa.Top + 4) sy = cursor.Y + 4;
        if (sx < wa.Left + 4) sx = cursor.X + 8;

        if (sx < wa.Left + 4) sx = wa.Left + 4;
        if (sy < wa.Top + 4) sy = wa.Top + 4;
        if (sx + size.Width > wa.Right - 4) sx = wa.Right - size.Width - 4;
        if (sy + size.Height > wa.Bottom - 4) sy = wa.Bottom - size.Height - 4;

        return new System.Windows.Point(sx, sy);
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
        if (_settings != null && _settings.IsVisible) { _settings.Activate(); return; }
        _settings = new SettingsWindow();
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    public void Quit()
    {
        _running = false;
        _window?.Close();
        _settings?.Close();
        _menu?.Close();
        if (_icon != null) _icon.Visible = false;
        System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
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
