using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;

namespace SnakeScreensaver.Snake;

public partial class SnakeGame
{
    private const double PoopLifetime = 3.8;
    private readonly List<PendingPoop> _pendingPoop = new();
    private readonly List<MagicDropping> _droppings = new();
    private Point _tailOutlet;
    private Vector _tailDirection;
    private bool _tailReady;
    private double _tailOpening;
    private double _tailHue = 280;
    private double _tailLastRelease = -100;
    private static readonly Geometry PoopCurl = CreatePoopGeometry("M -0.23,-0.23 C -0.3,-0.48 0.17,-0.4 0.08,-0.72 C 0.49,-0.51 0.38,-0.2 0.18,-0.17 Z");
    private static readonly Geometry PoopSparkle = CreatePoopGeometry("M 0,-1 L 0.22,-0.22 1,0 0.22,0.22 0,1 -0.22,0.22 -1,0 -0.22,-0.22 Z");

    private sealed class PendingPoop
    {
        public double Born, Hue;
        public int Growth;
    }

    private sealed class MagicDropping
    {
        public Point Origin;
        public Vector Direction;
        public double Born, Hue, Size;
    }

    private static Geometry CreatePoopGeometry(string path)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        return geometry;
    }

    private static double TurboDigestionDistance(double age)
    {
        var visibleAge = Math.Clamp(age, 0, DigestTime);
        return Cell * (1.1 + 7 * (1 - Math.Exp(-visibleAge * 0.85)))
            + Math.Max(0, age - DigestTime) * Cell * 7 * 0.85;
    }

    private void TurboResetPoop()
    {
        _pendingPoop.Clear();
        _droppings.Clear();
        _tailReady = false;
        _tailOpening = 0;
        _tailHue = 280;
        _tailLastRelease = -100;
    }

    private void TurboQueuePoop(Food food)
    {
        _pendingPoop.Add(new PendingPoop
        {
            Born = _mode == Settings.RenderMode.Balls ? _lastStepTime : Now,
            Hue = food.Hue,
            Growth = food.Growth
        });
    }

    private Point TurboSegmentPosition(int index, double t)
    {
        var segment = _segs[index];
        var progress = segment.MoveDuration > 0
            ? Math.Clamp((t - segment.MoveStart) / segment.MoveDuration, 0, 1) : 1;
        return new Point(segment.Stored.X + (segment.To.X - segment.Stored.X) * progress,
            segment.Stored.Y + (segment.To.Y - segment.Stored.Y) * progress);
    }

    private void TurboUpdatePoop(double t)
    {
        if (!Settings.MagicPoop)
        {
            TurboResetPoop();
            return;
        }
        _droppings.RemoveAll(drop => t - drop.Born >= PoopLifetime);
        _tailReady = false;
        Point tail, before;
        double length;
        if (_mode == Settings.RenderMode.Ribbon)
        {
            if (_ribbonSamples.Count < 2) return;
            tail = _ribbonSamples[^1];
            before = _ribbonSamples[Math.Max(0, _ribbonSamples.Count - 4)];
            length = 0;
            if (_pendingPoop.Count > 0)
                for (int i = 1; i < _ribbonSamples.Count; i++)
                    length += Len(_ribbonSamples[i - 1], _ribbonSamples[i]);
        }
        else
        {
            if (_segs.Count < 2) return;
            tail = TurboSegmentPosition(_segs.Count - 1, t);
            before = TurboSegmentPosition(_segs.Count - 2, t);
            length = (_segs.Count - 1) * Cell;
        }
        var direction = tail - before;
        if (direction.LengthSquared > 0.001) direction.Normalize();
        else direction = new Vector(-Math.Cos(_faceAngle), -Math.Sin(_faceAngle));
        _tailOutlet = tail + direction * Cell * 0.4;
        _tailDirection = direction;
        _tailReady = true;
        _tailOpening = Math.Clamp(1 - Math.Max(0, t - _tailLastRelease - 0.18) / 0.42, 0, 1);
        for (int i = _pendingPoop.Count - 1; i >= 0; i--)
        {
            var meal = _pendingPoop[i];
            var age = t - meal.Born - SwallowTime;
            if (age < 0) continue;
            var distance = TurboDigestionDistance(age);
            var anticipation = Math.Clamp(1 - (length - distance) / (Cell * 0.9), 0, 1);
            if (anticipation > _tailOpening)
            {
                _tailOpening = anticipation;
                _tailHue = MagicPoopHue(meal.Hue);
            }
            if (distance < length) continue;
            _tailLastRelease = t;
            _tailOpening = 1;
            _tailHue = MagicPoopHue(meal.Hue);
            _droppings.Add(new MagicDropping
            {
                Origin = _tailOutlet,
                Direction = direction,
                Born = t,
                Hue = meal.Hue,
                Size = Cell * (0.6 + meal.Growth * 0.04)
            });
            _pendingPoop.RemoveAt(i);
        }
    }

    private static int MagicPoopHue(double hue) => hue % 3 < 1 ? 185 : hue % 3 < 2 ? 280 : 315;

    private void TurboDrawTailPortal(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        if (!Settings.MagicPoop || !_tailReady) return;
        if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(
            new Rect(_tailOutlet.X - Cell, _tailOutlet.Y - Cell, Cell * 2, Cell * 2))) return;
        var opening = _tailOpening * _tailOpening * (3 - 2 * _tailOpening);
        var pulse = 1 + 0.04 * Math.Sin(t * 5);
        var rx = Cell * (0.12 + opening * 0.22) * pulse;
        var ry = Cell * (0.2 + opening * 0.23) * pulse;
        var hue = (int)_tailHue;
        dc.PushTransform(new TranslateTransform(_tailOutlet.X, _tailOutlet.Y));
        dc.PushTransform(new RotateTransform(Math.Atan2(_tailDirection.Y, _tailDirection.X) * 180 / Math.PI));
        if (Settings.DropShadow)
            dc.DrawEllipse(canvas.GetGlow(hue), null, new Point(), rx * 2.2, ry * 1.8);
        dc.DrawEllipse(canvas.GetFill(hue), canvas.GetStrokePen((hue + 325) % 360, 1.4), new Point(), rx, ry);
        for (int i = 0; i < 6; i++)
        {
            var angle = i * Math.PI / 3;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            dc.DrawEllipse(canvas.GetFill((hue + i * 9) % 360), canvas.GetStrokePen(hue, 0.8),
                new Point(cos * rx * 0.82, sin * ry * 0.82), Cell * 0.075, Cell * 0.09);
            dc.DrawLine(canvas.GetStrokePen((hue + 35) % 360, 1),
                new Point(cos * rx * 0.9, sin * ry * 0.9),
                new Point(cos * rx * 0.42, sin * ry * 0.42));
        }
        dc.DrawEllipse(Brushes.MidnightBlue, canvas.GetStrokePen((hue + 40) % 360, 1.2),
            new Point(), rx * (0.18 + opening * 0.5), ry * (0.12 + opening * 0.56));
        var sparkle = Cell * (0.055 + opening * 0.045) * (0.85 + 0.15 * Math.Sin(t * 12));
        dc.PushTransform(new ScaleTransform(sparkle, sparkle));
        dc.DrawGeometry(Brushes.White, null, PoopSparkle);
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private void TurboDrawPoop(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        if (!Settings.MagicPoop) return;
        foreach (var drop in _droppings)
        {
            var age = Math.Max(0, t - drop.Born);
            var release = 1 - Math.Exp(-age * 7);
            var bounce = age < 0.65 ? Math.Sin(age / 0.65 * Math.PI) * Cell * 0.28 : 0;
            var pos = drop.Origin + drop.Direction * (Cell * 0.65 * release);
            pos.Y += release * Cell * 0.3 - bounce;
            var size = drop.Size * (0.2 + 0.8 * Math.Min(1, age / 0.2));
            if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(pos.X - size, pos.Y - size, size * 2, size * 2))) continue;
            var fade = Math.Clamp((PoopLifetime - age) / 2, 0, 1);
            var hue = MagicPoopHue(drop.Hue);
            dc.PushOpacity(fade * fade * (0.88 + 0.12 * Math.Sin(age * 13 + drop.Hue)));
            if (Settings.DropShadow)
                dc.DrawEllipse(canvas.GetGlow(hue), null, pos, size, size * 0.8);
            dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
            dc.PushTransform(new RotateTransform(Math.Sin(age * 5 + drop.Hue) * 8 * (1 - age / PoopLifetime)));
            var pen = canvas.GetStrokePen((hue + 325) % 360, 1.2);
            dc.DrawEllipse(canvas.GetFill(hue), pen, new Point(0, size * 0.24), size * 0.5, size * 0.23);
            dc.DrawEllipse(canvas.GetFill((hue + 18) % 360), pen, new Point(-size * 0.025, 0), size * 0.37, size * 0.21);
            dc.PushTransform(new ScaleTransform(size, size));
            dc.DrawGeometry(canvas.GetFill((hue + 36) % 360), canvas.GetStrokePen((hue + 325) % 360, 0.05), PoopCurl);
            dc.Pop();
            dc.DrawEllipse(Brushes.White, null, new Point(-size * 0.17, -size * 0.06), size * 0.1, size * 0.035);
            dc.DrawEllipse(Brushes.White, null, new Point(-size * 0.22, size * 0.19), size * 0.12, size * 0.04);
            for (int i = 0; i < 3; i++)
            {
                var phase = age * 7 + i * 2.1 + drop.Hue;
                var sparkle = (0.5 + 0.5 * Math.Sin(phase)) * size * 0.14;
                dc.PushTransform(new TranslateTransform(size * (i - 1) * 0.65,
                    -size * (0.45 + 0.2 * Math.Cos(phase * 0.4))));
                dc.PushTransform(new ScaleTransform(sparkle, sparkle));
                dc.DrawGeometry(Brushes.White, null, PoopSparkle);
                dc.Pop();
                dc.Pop();
            }
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }
    }
}
