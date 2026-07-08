using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace SnakeScreensaver.Snake;

public partial class SnakeGame : UserControl
{
    private const double Cell = 30.0;

    private int _cols, _rows;
    private double _originX, _originY;

    private readonly List<(int X, int Y)> _snake = new();
    private readonly List<Seg> _segs = new();
    private readonly List<Food> _foods = new();
    private int _dirX = 1, _dirY = 0;
    private int _pendingGrowth;
    private double _intervalSec = 0.13;
    private double _lastStepTime;
    private bool _dead;
    private double _deadAt;
    private readonly Random _rng = new(42);
    private readonly Stopwatch _sw = new();
    private bool _started;

    public SnakeGame()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => InitFromSize(true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Start();
        _sw.Start();
        CompositionTarget.Rendering += OnRender;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRender;
    }

    private void InitFromSize(bool restartIfChanged = false)
    {
        var newCols = Math.Max(10, (int)(ActualWidth / Cell));
        var newRows = Math.Max(8, (int)(ActualHeight / Cell));
        _originX = (ActualWidth - newCols * Cell) / 2.0;
        _originY = (ActualHeight - newRows * Cell) / 2.0;
        if (restartIfChanged && _started && (newCols != _cols || newRows != _rows))
        {
            _cols = newCols; _rows = newRows;
            Start();
        }
        else
        {
            _cols = newCols; _rows = newRows;
        }
    }

    private double Now => _sw.Elapsed.TotalSeconds;

    private void OnRender(object? sender, EventArgs e)
    {
        if (_dead)
        {
            if (Now - _deadAt >= 1.7) Start();
            return;
        }
        if (_cols == 0) return;
        var t = Now;
        if (t - _lastStepTime >= _intervalSec)
        {
            _lastStepTime = t;
            Step();
        }
        UpdateColors(t);
    }

    private void Start()
    {
        InitFromSize();
        _started = true;
        Field.Children.Clear();
        _segs.Clear();
        _foods.Clear();
        _snake.Clear();
        _pendingGrowth = 0;
        _intervalSec = 0.13;

        BuildHaze();

        var cx = _cols / 2;
        var cy = _rows / 2;
        for (int i = 3; i >= 0; i--)
        {
            var cell = (X: cx - i, Y: cy);
            _snake.Add(cell);
            var seg = CreateSeg();
            var pos = CellCenter(cell);
            Canvas.SetLeft(seg.Ell, pos.X - seg.Ell.Width / 2);
            Canvas.SetTop(seg.Ell, pos.Y - seg.Ell.Height / 2);
            seg.Stored = pos;
            _segs.Add(seg);
        }
        _dirX = 1; _dirY = 0;

        for (int i = 0; i < 5; i++) SpawnFood();

        _dead = false;
        _lastStepTime = Now;
    }

    private Point CellCenter((int X, int Y) cell)
        => new(_originX + (cell.X + 0.5) * Cell, _originY + (cell.Y + 0.5) * Cell);

    private void Step()
    {
        var (dx, dy) = ChooseDir();
        if (_snake.Count > 1 && dx == -_dirX && dy == -_dirY) { dx = _dirX; dy = _dirY; }
        _dirX = dx; _dirY = dy;

        var head = _snake[0];
        var nh = (X: head.X + dx, Y: head.Y + dy);

        if (nh.X < 0 || nh.Y < 0 || nh.X >= _cols || nh.Y >= _rows) { Die(); return; }

        bool growing = _pendingGrowth > 0;
        var checkCount = growing ? _snake.Count : _snake.Count - 1;
        for (int i = 0; i < checkCount; i++)
            if (_snake[i].X == nh.X && _snake[i].Y == nh.Y) { Die(); return; }

        var foodIdx = _foods.FindIndex(f => f.Cell.X == nh.X && f.Cell.Y == nh.Y);
        if (foodIdx >= 0)
        {
            var f = _foods[foodIdx];
            _pendingGrowth += f.Growth;
            EatFood(f);
            SpawnFood();
        }

        _snake.Insert(0, nh);
        if (_pendingGrowth > 0)
        {
            _pendingGrowth--;
            AddSegVisual();
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }

        _intervalSec = Math.Max(0.045, 0.135 - _snake.Count * 0.0025);
        SyncSegs();
    }

    private void SyncSegs()
    {
        var dur = TimeSpan.FromSeconds(_intervalSec);
        for (int i = 0; i < _segs.Count; i++)
        {
            if (i >= _snake.Count) break;
            var seg = _segs[i];
            var target = CellCenter(_snake[i]);
            seg.AnimateTo(target, dur);
        }
    }

    private void AddSegVisual()
    {
        var seg = CreateSeg();
        var origin = _segs.Count > 0 ? _segs[0].Stored : CellCenter(_snake[0]);
        Canvas.SetLeft(seg.Ell, origin.X - seg.Ell.Width / 2);
        Canvas.SetTop(seg.Ell, origin.Y - seg.Ell.Height / 2);
        seg.Stored = origin;
        _segs.Insert(0, seg);
    }

    private (int dx, int dy) ChooseDir()
    {
        var head = _snake[0];
        if (_foods.Count == 0) return (_dirX, _dirY);

        var target = _foods.OrderBy(f => Math.Abs(f.Cell.X - head.X) + Math.Abs(f.Cell.Y - head.Y)).First().Cell;

        bool growing = _pendingGrowth > 0;
        var blocked = new HashSet<(int, int)>(growing ? _snake : _snake.Take(_snake.Count - 1).Select(c => (c.X, c.Y)));

        if (BfsFirst((head.X, head.Y), (target.X, target.Y), blocked, out var step))
            return step;

        var priority = new List<(int dx, int dy)> { (_dirX, _dirY), TurnLeft(_dirX, _dirY), TurnRight(_dirX, _dirY) };
        priority = priority.Where(d => !(d.dx == -_dirX && d.dy == -_dirY)).ToList();
        foreach (var (dx, dy) in priority)
        {
            var nx = head.X + dx;
            var ny = head.Y + dy;
            if (nx >= 0 && ny >= 0 && nx < _cols && ny < _rows && !blocked.Contains((nx, ny)))
                return (dx, dy);
        }
        return (_dirX, _dirY);
    }

    private static (int dx, int dy) TurnLeft(int dx, int dy) => (-dy, dx);
    private static (int dx, int dy) TurnRight(int dx, int dy) => (dy, -dx);

    private bool BfsFirst((int X, int Y) from, (int X, int Y) target, HashSet<(int, int)> blocked, out (int dx, int dy) step)
    {
        step = (0, 0);
        var prev = new Dictionary<(int, int), (int, int)>();
        var q = new Queue<(int, int)>();
        var s = (from.X, from.Y);
        prev[s] = (-1000, -1000);
        q.Enqueue(s);
        var opts = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur.Item1 == target.X && cur.Item2 == target.Y) break;
            foreach (var (ox, oy) in opts)
            {
                var n = (cur.Item1 + ox, cur.Item2 + oy);
                if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= _cols || n.Item2 >= _rows) continue;
                var isTarget = n.Item1 == target.X && n.Item2 == target.Y;
                if (!isTarget && blocked.Contains(n)) continue;
                if (!prev.ContainsKey(n))
                {
                    prev[n] = cur;
                    q.Enqueue(n);
                }
            }
        }

        if (!prev.ContainsKey((target.X, target.Y))) return false;
        var c = (target.X, target.Y);
        while (prev[c] != s) c = prev[c];
        step = (c.Item1 - from.X, c.Item2 - from.Y);
        return true;
    }

    private void Die()
    {
        _dead = true;
        _deadAt = Now;
        var n = _segs.Count;
        for (int i = 0; i < n; i++)
        {
            var seg = _segs[i];
            var hue = (Now * 55.0 + i * 11.0) % 360.0;
            TriggerBurst(seg.Stored, hue, 18);
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.55))
            {
                BeginTime = TimeSpan.FromSeconds(i * 0.012),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };
            seg.Ell.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void EatFood(Food f)
    {
        Field.Children.Remove(f.Shape);
        _foods.Remove(f);
        var pos = CellCenter(f.Cell);
        TriggerBurst(pos, f.Hue, 30);

        var head = _segs.FirstOrDefault();
        if (head == null) return;
        var scale = new ScaleTransform(1, 1) { CenterX = head.Half, CenterY = head.Half };
        head.Ell.RenderTransform = scale;
        var pop = new DoubleAnimation(1.0, 1.45, TimeSpan.FromSeconds(0.22))
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void TriggerBurst(Point center, double hue, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var ph = (hue + _rng.Next(-30, 30) + 360) % 360.0;
            var p = new Ellipse
            {
                Width = _rng.Next(7, 16),
                Height = _rng.Next(7, 16),
                Fill = new SolidColorBrush(Hsl(ph, 1.0, _rng.NextDouble() * 0.2 + 0.55)),
                Effect = new BlurEffect { Radius = _rng.Next(0, 5) }
            };
            var offX = -p.Width / 2;
            var offY = -p.Height / 2;
            Canvas.SetLeft(p, center.X + offX);
            Canvas.SetTop(p, center.Y + offY);
            Field.Children.Add(p);

            var ang = _rng.NextDouble() * Math.PI * 2;
            var spd = _rng.Next(40, 170);
            var dx = Math.Cos(ang) * spd;
            var dy = Math.Sin(ang) * spd;
            var dur = TimeSpan.FromSeconds(_rng.NextDouble() * 0.45 + 0.55);
            var ax = new DoubleAnimation(center.X + offX, center.X + offX + dx, dur)
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };
            var ay = new DoubleAnimation(center.Y + offY, center.Y + offY + dy, dur)
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };
            var ao = new DoubleAnimation(1, 0, dur) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } };
            var grow = new DoubleAnimation(1, 0.2, dur);

            var ell = p;
            ax.Completed += (_, _) => Field.Children.Remove(ell);
            ell.BeginAnimation(Canvas.LeftProperty, ax);
            ell.BeginAnimation(Canvas.TopProperty, ay);
            ell.BeginAnimation(OpacityProperty, ao);
            var st = new ScaleTransform(1, 1) { CenterX = p.Width / 2, CenterY = p.Height / 2 };
            ell.RenderTransform = st;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
    }

    private void UpdateColors(double t)
    {
        for (int i = 0; i < _segs.Count; i++)
        {
            var s = _segs[i];
            var hue = (t * 55.0 + i * 11.0) % 360.0;
            var core = Hsl(hue, 0.85, 0.82);
            var edge = Hsl(hue, 1.0, 0.5);
            s.Fill.GradientStops[0].Color = core;
            s.Fill.GradientStops[1].Color = edge;
            s.Stroke.Color = Hsl((hue + 320) % 360.0, 1.0, 0.6);
            s.Shadow.Color = Hsl(hue, 1.0, 0.55);
        }
    }

    private Seg CreateSeg()
    {
        var ell = new Ellipse
        {
            Width = Cell * 0.92,
            Height = Cell * 0.92,
            StrokeThickness = 2,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        var fill = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.35, 0.35),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        fill.GradientStops.Add(new GradientStop(Colors.White, 0));
        fill.GradientStops.Add(new GradientStop(Colors.Violet, 1));
        ell.Fill = fill;
        var stroke = new SolidColorBrush(Colors.White);
        ell.Stroke = stroke;
        var dse = new DropShadowEffect
        {
            Color = Colors.Magenta,
            BlurRadius = 26,
            ShadowDepth = 8,
            Direction = 320,
            Opacity = 0.95,
            RenderingBias = RenderingBias.Performance
        };
        ell.Effect = dse;
        Field.Children.Add(ell);
        return new Seg
        {
            Ell = ell,
            Fill = fill,
            Stroke = stroke,
            Shadow = dse,
            Half = ell.Width / 2
        };
    }

    private void SpawnFood()
    {
        var occupied = new HashSet<(int, int)>(_snake.Select(c => (c.X, c.Y)));
        foreach (var f in _foods) occupied.Add((f.Cell.X, f.Cell.Y));

        (int X, int Y)? cell = null;
        for (int i = 0; i < 80; i++)
        {
            var cand = (_rng.Next(_cols), _rng.Next(_rows));
            if (!occupied.Contains(cand)) { cell = cand; break; }
        }
        if (cell == null)
        {
            for (int y = 0; y < _rows; y++)
                for (int x = 0; x < _cols; x++)
                    if (!occupied.Contains((x, y))) { cell = (x, y); break; }
        }
        if (cell == null) return;
        CreateFood(cell.Value);
    }

    private void CreateFood((int X, int Y) cell)
    {
        var hue = _rng.NextDouble() * 360.0;
        var kind = _rng.Next(5);
        Shape sh;
        double w = Cell * 0.78, h = Cell * 0.78;

        if (kind == 0)
        {
            sh = new Ellipse { Width = w, Height = h };
        }
        else
        {
            var path = new Path
            {
                Width = w,
                Height = h,
                Stretch = Stretch.Fill,
                Data = FoodGeometry(kind)
            };
            sh = path;
        }

        var fill = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.4, 0.4),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.65,
            RadiusY = 0.65
        };
        fill.GradientStops.Add(new GradientStop(Hsl(hue, 0.85, 0.85), 0));
        fill.GradientStops.Add(new GradientStop(Hsl(hue, 1.0, 0.45), 1));
        sh.Fill = fill;

        sh.Stroke = new SolidColorBrush(Hsl((hue + 320) % 360.0, 1.0, 0.7));
        sh.StrokeThickness = 1.8;
        sh.UseLayoutRounding = true;

        var dse = new DropShadowEffect
        {
            Color = Hsl(hue, 1.0, 0.55),
            BlurRadius = 28,
            ShadowDepth = 4,
            Direction = 315,
            Opacity = 0.95,
            RenderingBias = RenderingBias.Performance
        };
        sh.Effect = dse;

        var pos = CellCenter(cell);
        Canvas.SetLeft(sh, pos.X - w / 2);
        Canvas.SetTop(sh, pos.Y - h / 2);
        sh.RenderTransformOrigin = new Point(0.5, 0.5);

        var group = new TransformGroup();
        var scale = new ScaleTransform(1, 1);
        var rot = new RotateTransform();
        group.Children.Add(scale);
        group.Children.Add(rot);
        sh.RenderTransform = group;

        rot.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(_rng.NextDouble() * 4 + 3))
            { RepeatBehavior = RepeatBehavior.Forever });

        var pulseDur = TimeSpan.FromSeconds(_rng.NextDouble() * 0.4 + 0.5);
        var sx = new DoubleAnimation(1.0, 1.2, pulseDur)
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        var sy = new DoubleAnimation(1.0, 1.2, pulseDur)
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);

        Field.Children.Add(sh);

        _foods.Add(new Food
        {
            Cell = cell,
            Hue = hue,
            Shape = sh,
            Growth = kind == 2 ? 3 : (kind == 1 ? 2 : 1)
        });
    }

    private static Geometry FoodGeometry(int kind)
    {
        var data = kind switch
        {
            1 => "M 9 1 L 11.4 6.6 L 17.7 6.6 L 12.6 10.4 L 14.5 16.4 L 9 12.8 L 3.5 16.4 L 5.4 10.4 L 0.3 6.6 L 6.6 6.6 Z",
            2 => "M 9 16.5 C 3.5 12.5 0.5 8 0.5 5 C 0.5 2.4 2.4 0.5 5 0.5 C 6.7 0.5 8.2 1.4 9 2.7 C 9.8 1.4 11.3 0.5 13 0.5 C 15.6 0.5 17.5 2.4 17.5 5 C 17.5 8 14.5 12.5 9 16.5 Z",
            3 => "M 9 0.5 L 17.5 9 L 9 17.5 L 0.5 9 Z",
            4 => "M 9 0.5 L 16.5 4.5 L 16.5 13.5 L 9 17.5 L 1.5 13.5 L 1.5 4.5 Z",
            _ => "M 9 0.5 L 17.5 9 L 9 17.5 L 0.5 9 Z"
        };
        return Geometry.Parse(data);
    }

    private void BuildHaze()
    {
        var hues = new[] { 0.0, 90.0, 180.0, 270.0 };
        for (int i = 0; i < hues.Length; i++)
        {
            var size = _rng.Next(700, 1100);
            var hue = (hues[i] + _rng.Next(-20, 20)) % 360.0;
            var brush = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(60, Hsl(hue, 1, 0.55).R, Hsl(hue, 1, 0.55).G, Hsl(hue, 1, 0.55).B), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, Hsl(hue, 1, 0.55).R, Hsl(hue, 1, 0.55).G, Hsl(hue, 1, 0.55).B), 1));

            var e = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                Opacity = 0.32
            };

            var startX = _rng.Next(-(int)e.Width / 2, Math.Max(0, (int)(ActualWidth - e.Width / 2)));
            var startY = _rng.Next(-(int)e.Height / 2, Math.Max(0, (int)(ActualHeight - e.Height / 2)));
            Canvas.SetLeft(e, startX);
            Canvas.SetTop(e, startY);

            var dur = TimeSpan.FromSeconds(_rng.Next(18, 34));
            var dx = _rng.Next(-(int)e.Width, (int)e.Width + (int)ActualWidth);
            var dy = _rng.Next(-(int)e.Height, (int)e.Height + (int)ActualHeight);
            e.BeginAnimation(Canvas.LeftProperty,
                new DoubleAnimation(startX, dx, dur)
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            e.BeginAnimation(Canvas.TopProperty,
                new DoubleAnimation(startY, dy, dur)
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });

            Field.Children.Add(e);
        }
    }

    private static Color Hsl(double h, double s, double l)
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
        return Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    private sealed class Seg
    {
        public Ellipse Ell = null!;
        public RadialGradientBrush Fill = null!;
        public SolidColorBrush Stroke = null!;
        public DropShadowEffect Shadow = null!;
        public double Half;
        public Point Stored;

        public void AnimateTo(Point center, TimeSpan dur)
        {
            var ax = new DoubleAnimation(Stored.X - Half, center.X - Half, dur);
            var ay = new DoubleAnimation(Stored.Y - Half, center.Y - Half, dur);
            Ell.BeginAnimation(Canvas.LeftProperty, ax);
            Ell.BeginAnimation(Canvas.TopProperty, ay);
            Stored = center;
        }
    }

    private sealed class Food
    {
        public (int X, int Y) Cell;
        public double Hue;
        public Shape Shape = null!;
        public int Growth;
    }
}
