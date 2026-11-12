using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SnakeScreensaver.Snake;

public partial class SnakeGame : UserControl
{
    private const double Cell = 30.0;

    private int _cols, _rows;
    private double _originX, _originY;

    private readonly Random _rng = new();
    private readonly Stopwatch _sw = new();
    private bool _started;
    private bool _dead;
    private double _deadAt;
    private double Now => _sw.Elapsed.TotalSeconds;

    private Settings.RenderMode _mode = Settings.RenderMode.Balls;
    private bool _rainbow = true;
    private double _hueOffset = 0;

    private readonly List<(int X, int Y)> _snake = new();
    private readonly List<Seg> _segs = new();
    private readonly List<Food> _foods = new();
    private int _dirX = 1, _dirY = 0;
    private int _pendingGrowth;
    private double _intervalSec = 0.13;
    private double _lastStepTime;

    private readonly List<Point> _ribbonSamples = new();
    private (int dx, int dy) _ribbonDir = (1, 0);
    private (int X, int Y) _ribbonHeadCell;
    private double _ribbonTargetLenPx;
    private double _ribbonSpeed = 230;
    private const double RibbonSpacing = 4.0;
    private double _ribbonLastT;
    private bool _ribbonReady;

    public SnakeGame()
    {
        InitializeComponent();
        _mode = Settings.Mode;
        _rainbow = Settings.Rainbow;
        TurboCanvas.Host = this;
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
        TurboResize();
    }

    private void OnRender(object? sender, EventArgs e)
    {
        var t = Now;
        if (_dead)
        {
            if (t - _deadAt >= 1.7) Start();
            else TurboRedraw(t);
            return;
        }
        if (_cols == 0) return;
        if (_mode == Settings.RenderMode.Balls)
        {
            _lastStepTime = Math.Max(_lastStepTime, t - 0.25);
            while (!_dead && t - _lastStepTime >= _intervalSec)
            {
                _lastStepTime += _intervalSec;
                StepBalls();
            }
        }
        else
        {
            UpdateRibbon(t);
        }
        TurboRedraw(t);
    }

    private void Start()
    {
        InitFromSize();
        _hueOffset = _rng.NextDouble() * 360.0;
        _started = true;
        _segs.Clear();
        _foods.Clear();
        _snake.Clear();
        _pendingGrowth = 0;
        _intervalSec = 0.13;
        _ribbonSamples.Clear();
        _ribbonReady = false;

        TurboReset();

        if (_mode == Settings.RenderMode.Balls) StartBalls();
        else StartRibbon();

        for (int i = 0; i < 5; i++) SpawnFood();

        _dead = false;
        _lastStepTime = Now;
        _ribbonLastT = Now;
    }

    private void StartBalls()
    {
        var cx = _cols / 2;
        var cy = _rows / 2;
        for (int i = 3; i >= 0; i--)
        {
            var cell = (X: cx - i, Y: cy);
            _snake.Add(cell);
            var pos = CellCenter(cell);
            var seg = TurboCreateSeg();
            seg.Stored = pos;
            seg.To = pos;
            seg.MoveStart = Now;
            seg.MoveDuration = _intervalSec;
            _segs.Add(seg);
        }
        _dirX = 1; _dirY = 0;
    }

    private void StartRibbon()
    {
        _ribbonTargetLenPx = 4 * Cell;
        _ribbonSpeed = 230;
        _ribbonDir = (1, 0);
        var cx = _cols / 2;
        var cy = _rows / 2;
        var headPos = CellCenter((cx, cy));
        _ribbonSamples.Clear();
        _ribbonSamples.Add(headPos);
        var backDx = -_ribbonDir.dx;
        var backDy = -_ribbonDir.dy;
        var n = (int)Math.Ceiling((_ribbonTargetLenPx + Cell) / RibbonSpacing) + 1;
        for (int i = 1; i < n; i++)
            _ribbonSamples.Add(new Point(headPos.X + backDx * RibbonSpacing * i, headPos.Y + backDy * RibbonSpacing * i));
        _ribbonHeadCell = (cx, cy);
        _ribbonReady = true;
        _ribbonLastT = Now;
    }

    private Point CellCenter((int X, int Y) cell)
        => new(_originX + (cell.X + 0.5) * Cell, _originY + (cell.Y + 0.5) * Cell);

    private (int X, int Y) PixelToCell(Point p)
    {
        var x = (int)((p.X - _originX) / Cell);
        var y = (int)((p.Y - _originY) / Cell);
        return (x, y);
    }

    private static double Len(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void StepBalls()
    {
        var (dx, dy) = ChooseDirBalls();
        if (_snake.Count > 1 && dx == -_dirX && dy == -_dirY) { dx = _dirX; dy = _dirY; }
        _dirX = dx; _dirY = dy;

        var head = _snake[0];
        var nh = (X: head.X + dx, Y: head.Y + dy);

        if (nh.X < 0 || nh.Y < 0 || nh.X >= _cols || nh.Y >= _rows) { DieBalls(); return; }

        bool growing = _pendingGrowth > 0;
        var checkCount = growing ? _snake.Count : _snake.Count - 1;
        for (int i = 0; i < checkCount; i++)
            if (_snake[i].X == nh.X && _snake[i].Y == nh.Y) { DieBalls(); return; }

        var foodIdx = _foods.FindIndex(f => f.Cell.X == nh.X && f.Cell.Y == nh.Y);
        if (foodIdx >= 0)
        {
            var f = _foods[foodIdx];
            _pendingGrowth += f.Growth;
            TurboEatFood(f);
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
        if (foodIdx >= 0) SpawnFood();

        SyncSegs();
    }

    private void SyncSegs()
    {
        for (int i = 0; i < _segs.Count; i++)
        {
            if (i >= _snake.Count) break;
            var seg = _segs[i];
            seg.Stored = seg.To;
            seg.To = CellCenter(_snake[i]);
            seg.MoveStart = _lastStepTime;
            seg.MoveDuration = _intervalSec;
        }
    }

    private void AddSegVisual()
    {
        var origin = _segs.Count > 0 ? _segs[0].To : CellCenter(_snake[0]);
        var seg = TurboCreateSeg();
        seg.Stored = origin;
        seg.To = origin;
        seg.MoveStart = Now;
        seg.MoveDuration = _intervalSec;
        _segs.Insert(0, seg);
    }

    private (int dx, int dy) ChooseDirBalls()
    {
        var head = _snake[0];
        if (_foods.Count == 0) return (_dirX, _dirY);
        var target = _foods.OrderBy(f => Math.Abs(f.Cell.X - head.X) + Math.Abs(f.Cell.Y - head.Y)).First().Cell;

        bool growing = _pendingGrowth > 0;
        var blocked = new HashSet<(int, int)>(growing ? _snake : _snake.Take(_snake.Count - 1).Select(c => (c.X, c.Y)));
        if (BfsFirst((head.X, head.Y), (target.X, target.Y), blocked, out var step)) return step;

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

    private void UpdateRibbon(double t)
    {
        if (!_ribbonReady || _ribbonSamples.Count < 2) return;
        var dt = Math.Max(0.0001, Math.Min(0.05, t - _ribbonLastT));
        _ribbonLastT = t;

        var head = _ribbonSamples[0];
        var newHead = new Point(head.X + _ribbonDir.dx * _ribbonSpeed * dt, head.Y + _ribbonDir.dy * _ribbonSpeed * dt);
        _ribbonSamples.Insert(0, newHead);

        var nc = PixelToCell(newHead);
        if (nc.X != _ribbonHeadCell.X || nc.Y != _ribbonHeadCell.Y)
        {
            OnRibbonCellCrossed(nc);
            if (_dead) return;
        }
        _ribbonHeadCell = nc;

        TrimRibbonTo(_ribbonTargetLenPx);
    }

    private void OnRibbonCellCrossed((int X, int Y) nc)
    {
        if (nc.X < 0 || nc.Y < 0 || nc.X >= _cols || nc.Y >= _rows) { DieRibbon(); return; }

        var body = BodyCellsFromTail();
        if (body.Contains((nc.X, nc.Y))) { DieRibbon(); return; }

        var foodIdx = _foods.FindIndex(f => f.Cell.X == nc.X && f.Cell.Y == nc.Y);
        if (foodIdx >= 0)
        {
            var f = _foods[foodIdx];
            _ribbonTargetLenPx += f.Growth * Cell;
            TurboEatFood(f);
            SpawnFood();
        }

        var newDir = ChooseDirForRibbon(nc, body);
        if (newDir.dx == -_ribbonDir.dx && newDir.dy == -_ribbonDir.dy) newDir = _ribbonDir;
        _ribbonDir = newDir;
    }

    private HashSet<(int, int)> BodyCellsFromTail()
    {
        var set = new HashSet<(int, int)>();
        for (int i = 1; i < _ribbonSamples.Count; i++)
        {
            var c = PixelToCell(_ribbonSamples[i]);
            set.Add((c.X, c.Y));
        }
        return set;
    }

    private (int dx, int dy) ChooseDirForRibbon((int X, int Y) head, HashSet<(int, int)> blocked)
    {
        if (_foods.Count == 0) return _ribbonDir;
        var target = _foods.OrderBy(f => Math.Abs(f.Cell.X - head.X) + Math.Abs(f.Cell.Y - head.Y)).First().Cell;
        if (BfsFirst((head.X, head.Y), (target.X, target.Y), blocked, out var step)) return step;

        var priority = new List<(int dx, int dy)> { _ribbonDir, TurnLeft(_ribbonDir.dx, _ribbonDir.dy), TurnRight(_ribbonDir.dx, _ribbonDir.dy) };
        priority = priority.Where(d => !(d.dx == -_ribbonDir.dx && d.dy == -_ribbonDir.dy)).ToList();
        foreach (var (dx, dy) in priority)
        {
            var nx = head.X + dx;
            var ny = head.Y + dy;
            if (nx >= 0 && ny >= 0 && nx < _cols && ny < _rows && !blocked.Contains((nx, ny)))
                return (dx, dy);
        }
        return _ribbonDir;
    }

    private void TrimRibbonTo(double target)
    {
        if (_ribbonSamples.Count < 2) return;
        double acc = 0;
        double prevAcc = 0;
        int cutIndex = -1;
        for (int i = 1; i < _ribbonSamples.Count; i++)
        {
            var d = Len(_ribbonSamples[i - 1], _ribbonSamples[i]);
            prevAcc = acc;
            acc += d;
            if (acc >= target) { cutIndex = i; break; }
        }
        if (cutIndex < 0) return;

        var a = _ribbonSamples[cutIndex - 1];
        var b = _ribbonSamples[cutIndex];
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var dlen = Math.Sqrt(dx * dx + dy * dy);
        var remain = target - prevAcc;
        var ratio = dlen > 0 ? remain / dlen : 0;
        if (ratio < 0) ratio = 0;
        _ribbonSamples[cutIndex] = new Point(a.X + dx * ratio, a.Y + dy * ratio);
        while (_ribbonSamples.Count > cutIndex + 1) _ribbonSamples.RemoveAt(_ribbonSamples.Count - 1);
    }

    private void DieBalls()
    {
        _dead = true;
        _deadAt = Now;
        for (int i = 0; i < _segs.Count; i++)
        {
            var hue = (Now * 55.0 + i * 11.0) % 360.0;
            TurboBurst(_segs[i].To, hue, 18);
        }
    }

    private void DieRibbon()
    {
        _dead = true;
        _deadAt = Now;
        var samples = _ribbonSamples.ToList();
        for (int i = 0; i < samples.Count; i += Math.Max(1, samples.Count / 16))
        {
            var hue = (i * 11.0 + Now * 55.0) % 360.0;
            TurboBurst(samples[i], hue, 12);
        }
    }

    private void SpawnFood()
    {
        IEnumerable<(int, int)> baseOcc = _mode == Settings.RenderMode.Ribbon
            ? BodyCellsFromTail()
            : _snake.Select(c => (c.X, c.Y));
        var occupied = new HashSet<(int, int)>(baseOcc);
        if (_mode == Settings.RenderMode.Ribbon && _ribbonSamples.Count > 0)
        {
            var head = PixelToCell(_ribbonSamples[0]);
            occupied.Add((head.X, head.Y));
        }
        foreach (var f in _foods) occupied.Add((f.Cell.X, f.Cell.Y));

        (int X, int Y)? cell = null;
        for (int i = 0; i < 80; i++)
        {
            var cand = (_rng.Next(_cols), _rng.Next(_rows));
            if (!occupied.Contains(cand)) { cell = cand; break; }
        }
        if (cell == null)
        {
            for (int y = 0; y < _rows && cell == null; y++)
                for (int x = 0; x < _cols && cell == null; x++)
                    if (!occupied.Contains((x, y))) cell = (x, y);
        }
        if (cell == null) return;
        CreateFood(cell.Value);
    }

    private void CreateFood((int X, int Y) cell)
    {
        var hue = _rng.NextDouble() * 360.0;
        var kind = _rng.Next(5);
        var growth = kind == 2 ? 3 : (kind == 1 ? 2 : 1);
        var food = new Food
        {
            Cell = cell,
            Hue = hue,
            Kind = kind,
            Growth = growth,
            PulseFreq = 2 * Math.PI / (_rng.NextDouble() * 0.4 + 0.5),
            RotSpeed = 1.0 / (_rng.NextDouble() * 4 + 3)
        };
        TurboAddFood(food);
        _foods.Add(food);
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

    private sealed class Food
    {
        public (int X, int Y) Cell;
        public double Hue;
        public int Kind;
        public int Growth;
        public double SpawnTime;
        public double PulseFreq;
        public double RotSpeed;
    }

    private sealed class Seg
    {
        public double Half;
        public Point Stored;
        public Point To;
        public double MoveStart;
        public double MoveDuration;
    }
}
