using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SnakeScreensaver;

public partial class TrayMenuWindow : Window
{
    private readonly TrayHost _host;
    private double _anchorRight;
    private double _anchorBottom;

    public TrayMenuWindow(TrayHost host)
    {
        InitializeComponent();
        _host = host;
        BuildIdleOptions();
        SyncRadios();
        Loaded += TrayMenuWindow_Loaded;

        RenderExp.Expanded += Expander_StateChanged;
        RenderExp.Collapsed += Expander_StateChanged;
        ExitExp.Expanded += Expander_StateChanged;
        ExitExp.Collapsed += Expander_StateChanged;
        IdleExp.Expanded += Expander_StateChanged;
        IdleExp.Collapsed += Expander_StateChanged;
    }

    private void TrayMenuWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var anim = new DoubleAnimation(0.6, 1.0, System.TimeSpan.FromSeconds(0.18))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private void SyncRadios()
    {
        RenderBalls.IsChecked = Settings.Mode == Settings.RenderMode.Balls;
        RenderRibbon.IsChecked = Settings.Mode == Settings.RenderMode.Ribbon;
        ExitEsc.IsChecked = Settings.Exit == Settings.ExitMode.EscapeOnly;
        ExitMouse.IsChecked = Settings.Exit == Settings.ExitMode.MouseMove;
        UpdateRainbowLabel();
    }

    private void UpdateRainbowLabel()
    {
        RainbowLabel.Text = Settings.Rainbow ? "RAINBOW: ON" : "RAINBOW: OFF";
    }

    private readonly int[] _idleOptions = { 0, 1, 3, 5, 10, 30, 60 };
    private void BuildIdleOptions()
    {
        foreach (var v in _idleOptions)
        {
            var rb = new RadioButton
            {
                Content = v == 0 ? "OFF (manual only)" : $"{v} min after idle",
                GroupName = "idle",
                Tag = v,
                Style = (Style)FindResource("TrayRadio"),
                IsChecked = Settings.IdleMinutes == v,
                Margin = new Thickness(0, 4, 0, 4)
            };
            rb.Checked += Idle_CheckedChanged;
            IdlePanel.Children.Add(rb);
        }
    }

    private void Idle_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.IsChecked == true && rb.Tag is int v)
        {
            Settings.IdleMinutes = v;
            Settings.Save();
        }
    }

    private void Render_Checked(object sender, RoutedEventArgs e)
    {
        if (RenderBalls.IsChecked == true) Settings.Mode = Settings.RenderMode.Balls;
        else if (RenderRibbon.IsChecked == true) Settings.Mode = Settings.RenderMode.Ribbon;
        Settings.Save();
    }

    private void Exit_Checked(object sender, RoutedEventArgs e)
    {
        if (ExitEsc.IsChecked == true) Settings.Exit = Settings.ExitMode.EscapeOnly;
        else if (ExitMouse.IsChecked == true) Settings.Exit = Settings.ExitMode.MouseMove;
        Settings.Save();
    }

    private void Rainbow_Click(object sender, RoutedEventArgs e)
    {
        Settings.Rainbow = !Settings.Rainbow;
        UpdateRainbowLabel();
        Settings.Save();
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        _host.LaunchSnake();
        Close();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        Log.Line("TrayMenu.Settings_Click");
        Deactivated -= Window_Deactivated;
        _host.OpenSettings();
        Close();
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        Log.Line("TrayMenu.Quit_Click");
        Deactivated -= Window_Deactivated;
        _host.Quit();
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_Deactivated(object? sender, System.EventArgs e)
    {
        Log.Line("TrayMenu.Window_Deactivated → Close");
        Close();
    }

    internal void InitAnchor(double right, double bottom, System.Windows.Size size)
    {
        _anchorRight = right;
        _anchorBottom = bottom;
        Log.Line($"InitAnchor: right={right} bottom={bottom} size={size.Width}x{size.Height}");
    }

    internal System.Windows.Point ComputeRestLocation()
    {
        var wa = SystemParameters.WorkArea;
        var w = ActualWidth > 0 ? ActualWidth : (RenderSize.Width > 0 ? RenderSize.Width : 360);
        var h = ActualHeight > 0 ? ActualHeight : (RenderSize.Height > 0 ? RenderSize.Height : 480);
        double left = _anchorRight - w;
        double top = _anchorBottom - h;
        if (top < wa.Top + 4) top = wa.Top + 4;
        if (left < wa.Left + 4) left = wa.Left + 4;
        return new System.Windows.Point(left, top);
    }

    private void Expander_StateChanged(object sender, RoutedEventArgs e)
    {
        var exp = sender as Expander;
        Log.Line($"Expander_StateChanged: {exp?.Name} IsExpanded={exp?.IsExpanded} anchors=({_anchorRight},{_anchorBottom})");
        Dispatcher.BeginInvoke(new Action(Reposition), DispatcherPriority.Background);
    }

    private void Reposition()
    {
        try
        {
            if (_anchorRight <= 0 || _anchorBottom <= 0)
            {
                Log.Line("Reposition: skipped (no anchors yet)");
                return;
            }
            var loc = ComputeRestLocation();
            Left = loc.X;
            Top = loc.Y;
            Log.Line($"Reposition: ({Left},{Top}) size {ActualWidth}x{ActualHeight} anchors=({_anchorRight},{_anchorBottom})");
        }
        catch (Exception ex)
        {
            Log.Exc("Reposition threw", ex);
        }
    }
}
