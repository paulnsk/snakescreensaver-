using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SnakeScreensaver;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        RendererWpf.IsChecked = Settings.Render == Settings.Renderer.Wpf;
        RendererSkia.IsChecked = Settings.Render == Settings.Renderer.Skia;
        RendererWpfTurbo.IsChecked = Settings.Render == Settings.Renderer.WpfTurbo;
        RenderBalls.IsChecked = Settings.Mode == Settings.RenderMode.Balls;
        RenderRibbon.IsChecked = Settings.Mode == Settings.RenderMode.Ribbon;
        ExitEsc.IsChecked = Settings.Exit == Settings.ExitMode.EscapeOnly;
        ExitMouse.IsChecked = Settings.Exit == Settings.ExitMode.MouseMove;
        RainbowChk.IsChecked = Settings.Rainbow;
        DropShadowChk.IsChecked = Settings.DropShadow;
        IdleSlider.Value = Settings.IdleMinutes;
        StartupChk.IsChecked = Settings.RunAtStartup;
    }

    private void Renderer_Checked(object sender, RoutedEventArgs e)
    {
        if (RendererWpf.IsChecked == true) Settings.Render = Settings.Renderer.Wpf;
        else if (RendererSkia.IsChecked == true) Settings.Render = Settings.Renderer.Skia;
        else if (RendererWpfTurbo.IsChecked == true) Settings.Render = Settings.Renderer.WpfTurbo;
    }

    private void Render_Checked(object sender, RoutedEventArgs e)
    {
        if (RenderBalls.IsChecked == true) Settings.Mode = Settings.RenderMode.Balls;
        else if (RenderRibbon.IsChecked == true) Settings.Mode = Settings.RenderMode.Ribbon;
    }

    private void Exit_Checked(object sender, RoutedEventArgs e)
    {
        if (ExitEsc.IsChecked == true) Settings.Exit = Settings.ExitMode.EscapeOnly;
        else if (ExitMouse.IsChecked == true) Settings.Exit = Settings.ExitMode.MouseMove;
    }

    private void Rainbow_Changed(object sender, RoutedEventArgs e)
    {
        Settings.Rainbow = RainbowChk.IsChecked == true;
    }

    private void DropShadow_Changed(object sender, RoutedEventArgs e)
    {
        Settings.DropShadow = DropShadowChk.IsChecked == true;
    }

    private void Idle_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Settings.IdleMinutes = (int)IdleSlider.Value;
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        Settings.RunAtStartup = StartupChk.IsChecked == true;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Settings.Save();
        App.Tray.RestartSnakeIfVisible();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Settings.Load();
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Cancel_Click(sender, e); e.Handled = true; }
    }
}
