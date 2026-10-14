using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SnakeScreensaver;

public partial class MainWindow
{
    private void CreateTurboDisplays()
    {
        var screens = System.Windows.Forms.Screen.AllScreens.OrderByDescending(s => s.Primary).ToArray();
        if (screens.Length < 2) return;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        Game.Width = SystemParameters.VirtualScreenWidth;
        Game.Height = SystemParameters.VirtualScreenHeight;
        Game.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        Game.VerticalAlignment = System.Windows.VerticalAlignment.Top;

        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i].Bounds;
            var topLeft = transform.Transform(new Point(screen.Left, screen.Top));
            var bottomRight = transform.Transform(new Point(screen.Right, screen.Bottom));
            var bounds = new Rect(topLeft, bottomRight);
            var world = new Rect(bounds.Left - virtualLeft, bounds.Top - virtualTop, bounds.Width, bounds.Height);
            if (i == 0)
            {
                Left = bounds.Left;
                Top = bounds.Top;
                Width = bounds.Width;
                Height = bounds.Height;
                Game.RenderTransform = new TranslateTransform(-world.Left, -world.Top);
                Game.SetTurboViewport(world);
                continue;
            }

            var view = new Window
            {
                Owner = this,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                Cursor = Cursor,
                Content = Game.CreateTurboViewport(world)
            };
            view.PreviewKeyDown += Window_PreviewKeyDown;
            if (Settings.Exit == Settings.ExitMode.MouseMove)
            {
                view.MouseMove += MainWindow_MouseMove;
                view.MouseDown += (_, _) => Close();
            }
            view.Show();
        }
    }
}
