using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SnakeScreensaver.Snake;

public partial class SnakeGame
{
    private const double SwallowTime = 0.22;
    private const double DigestTime = 4.0;
    private readonly List<Meal> _meals = new();
    private double _faceAngle;
    private double _faceLastTime;
    private double _lastBite = -100;

    private sealed class Meal
    {
        public Food Food = null!;
        public Point Origin;
        public double Born, Age, Center, Amplitude, Width;
    }

    private void TurboResetFeeding()
    {
        _meals.Clear();
        _faceAngle = 0;
        _faceLastTime = Now;
        _lastBite = -100;
    }

    private void TurboSwallow(Food food)
    {
        var born = _mode == Settings.RenderMode.Balls ? _lastStepTime : Now;
        _lastBite = born;
        _meals.Add(new Meal
        {
            Food = food,
            Origin = CellCenter(food.Cell),
            Born = born,
            Width = Cell * (0.75 + food.Growth * 0.06)
        });
        if (_meals.Count > 12) _meals.RemoveAt(0);
        TurboBurst(CellCenter(food.Cell), food.Hue, 9);
        for (int i = Math.Max(0, _turboParticles.Count - 9); i < _turboParticles.Count; i++)
        {
            _turboParticles[i].Size *= 0.4;
            _turboParticles[i].Vx *= 0.6;
            _turboParticles[i].Vy *= 0.6;
            _turboParticles[i].Life *= 0.65;
        }
    }

    private Point TurboHeadPosition(double t)
    {
        if (_mode == Settings.RenderMode.Ribbon)
            return _ribbonSamples.Count > 0 ? _ribbonSamples[0] : new Point();
        if (_segs.Count == 0) return new Point();
        var head = _segs[0];
        var progress = head.MoveDuration > 0 ? Math.Clamp((t - head.MoveStart) / head.MoveDuration, 0, 1) : 1;
        return new Point(head.Stored.X + (head.To.X - head.Stored.X) * progress,
                         head.Stored.Y + (head.To.Y - head.Stored.Y) * progress);
    }

    private void TurboUpdateFeeding(double t)
    {
        if (!Settings.LivingSnake)
        {
            _meals.Clear();
            return;
        }
        var dx = (double)_dirX;
        var dy = (double)_dirY;
        if (_mode == Settings.RenderMode.Ribbon)
        {
            dx = _ribbonDir.dx;
            dy = _ribbonDir.dy;
        }
        else if (_segs.Count > 0 && Len(_segs[0].Stored, _segs[0].To) > 0.01)
        {
            dx = _segs[0].To.X - _segs[0].Stored.X;
            dy = _segs[0].To.Y - _segs[0].Stored.Y;
        }
        var targetAngle = Math.Atan2(dy, dx);
        var delta = Math.Atan2(Math.Sin(targetAngle - _faceAngle), Math.Cos(targetAngle - _faceAngle));
        var dt = Math.Clamp(t - _faceLastTime, 0, 0.25);
        _faceAngle += delta * (1 - Math.Exp(-dt * 24));
        _faceLastTime = t;

        for (int i = _meals.Count - 1; i >= 0; i--)
        {
            var meal = _meals[i];
            meal.Age = Math.Max(0, t - meal.Born);
            var age = meal.Age - SwallowTime;
            if (age >= DigestTime) { _meals.RemoveAt(i); continue; }
            var progress = Math.Clamp(age / DigestTime, 0, 1);
            meal.Center = Cell * (1.1 + 7 * (1 - Math.Exp(-Math.Max(0, age) * 0.85)));
            meal.Amplitude = Math.Clamp(age / 0.25, 0, 1) * Math.Pow(1 - progress, 1.4)
                * (0.55 + Math.Min(0.35, meal.Food.Growth * 0.07));
        }
    }

    private double TurboDigestScale(double distance, ref double hue)
    {
        if (!Settings.LivingSnake) return 1;
        var swell = 0.0;
        var strongest = 0.0;
        foreach (var meal in _meals)
        {
            var d = (distance - meal.Center) / meal.Width;
            if (Math.Abs(d) > 3) continue;
            var amount = meal.Amplitude * Math.Exp(-d * d * 0.5);
            swell += amount;
            if (amount > strongest)
            {
                strongest = amount;
                var delta = (meal.Food.Hue - hue + 540) % 360 - 180;
                hue = (hue + delta * amount * 0.65 + 360) % 360;
            }
        }
        return 1 + Math.Min(0.85, swell);
    }

    private void TurboDrawSwallows(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        var head = TurboHeadPosition(t);
        var mouth = new Point(head.X + Math.Cos(_faceAngle) * Cell * 0.36,
                              head.Y + Math.Sin(_faceAngle) * Cell * 0.36);
        foreach (var meal in _meals)
        {
            if (meal.Age >= SwallowTime) continue;
            var progress = Math.Clamp(meal.Age / SwallowTime, 0, 1);
            var pull = 1 - Math.Pow(1 - progress, 3);
            var pos = new Point(meal.Origin.X + (mouth.X - meal.Origin.X) * pull,
                                meal.Origin.Y + (mouth.Y - meal.Origin.Y) * pull);
            var size = Cell * 0.78 * (1 - progress * 0.95);
            if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(pos.X - size, pos.Y - size, size * 2, size * 2))) continue;
            dc.PushOpacity(1 - progress * progress);
            if (Settings.DropShadow)
                dc.DrawEllipse(canvas.GetGlow((int)meal.Food.Hue), null, pos, size * 0.8, size * 0.8);
            dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
            dc.PushTransform(new RotateTransform(progress * 240));
            if (meal.Food.Kind == 0)
                dc.DrawEllipse(canvas.GetFill((int)meal.Food.Hue), canvas.GetStrokePen((int)meal.Food.Hue, 1.8), new Point(), size / 2, size / 2);
            else
            {
                dc.PushTransform(new ScaleTransform(size / 17.5, size / 17.5));
                dc.PushTransform(new TranslateTransform(-9, -9));
                dc.DrawGeometry(canvas.GetFill((int)meal.Food.Hue), canvas.GetStrokePen((int)meal.Food.Hue, 1.8), canvas.GetFoodGeometry(meal.Food.Kind));
                dc.Pop();
                dc.Pop();
            }
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }
    }

    private void TurboDrawFace(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        var head = TurboHeadPosition(t);
        var r = Cell * 0.57;
        if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(head.X - r * 2, head.Y - r * 2, r * 4, r * 4))) return;
        var cos = Math.Cos(_faceAngle);
        var sin = Math.Sin(_faceAngle);
        var gaze = new Point(r, 0);
        var nearest = double.MaxValue;
        var anticipation = 0.0;
        foreach (var food in _foods)
        {
            var pos = CellCenter(food.Cell);
            var distance = Len(head, pos);
            if (distance >= nearest) continue;
            nearest = distance;
            gaze = new Point(pos.X - head.X, pos.Y - head.Y);
        }
        if (nearest < Cell * 3 && gaze.X * cos + gaze.Y * sin > 0)
            anticipation = Math.Clamp(1 - nearest / (Cell * 3), 0, 1);
        var chew = Math.Max(0, t - _lastBite);
        var opening = 0.12 + anticipation * 0.85;
        if (chew < 0.32) opening = 0.12 + 0.9 * Math.Pow(1 - chew / 0.32, 1.5);
        var angle = (6 + opening * 43) * Math.PI / 180;
        var hue = (int)((_hueOffset + (_rainbow ? t * 55 : 0)) % 360);
        r *= 1 + (chew < 0.5 ? Math.Sin(chew / 0.5 * Math.PI) * 0.07 : 0);

        if (Settings.DropShadow)
        {
            var glow = canvas.GetGlowImage(hue, Cell * 0.46 * 1.45);
            var size = glow.Width * r / (Cell * 0.46);
            dc.DrawImage(glow, new Rect(head.X + 4 - size / 2, head.Y + 6 - size / 2, size, size));
        }
        dc.PushTransform(new TranslateTransform(head.X, head.Y));
        dc.PushTransform(new RotateTransform(_faceAngle * 180 / Math.PI));
        var flip = cos < 0 ? -1 : 1;
        dc.PushTransform(new ScaleTransform(1, flip));
        var upper = new Point(r * Math.Cos(angle), -r * Math.Sin(angle));
        var lower = new Point(upper.X, -upper.Y);
        var face = new StreamGeometry();
        using (var ctx = face.Open())
        {
            ctx.BeginFigure(new Point(-r * 0.16, 0), true, true);
            ctx.LineTo(upper, true, false);
            ctx.ArcTo(lower, new System.Windows.Size(r, r), 0, true, SweepDirection.Counterclockwise, true, true);
        }
        face.Freeze();
        dc.DrawGeometry(canvas.GetFill(hue), canvas.GetStrokePen(hue, 1.8), face);

        if (opening > 0.35)
            dc.DrawEllipse(System.Windows.Media.Brushes.HotPink, null, new Point(r * 0.45, r * 0.12), r * 0.22, r * 0.1);
        var eye = new Point(-r * 0.05, -r * 0.53);
        var ink = new System.Windows.Media.Pen(System.Windows.Media.Brushes.MidnightBlue, 1.3);
        ink.Freeze();
        var shine = new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 1.7);
        shine.Freeze();
        if (chew > 0.28 && chew < 0.58)
            dc.DrawLine(ink, new Point(eye.X - r * 0.23, eye.Y + r * 0.07), new Point(eye.X + r * 0.23, eye.Y - r * 0.07));
        else
        {
            dc.DrawEllipse(System.Windows.Media.Brushes.White, ink, eye, r * 0.24, r * 0.28);
            var length = Math.Max(1, Math.Sqrt(gaze.X * gaze.X + gaze.Y * gaze.Y));
            var pupil = new Point(eye.X + (gaze.X * cos + gaze.Y * sin) / length * r * 0.1,
                eye.Y + (-gaze.X * sin + gaze.Y * cos) / length * r * 0.1 * flip);
            dc.DrawEllipse(System.Windows.Media.Brushes.MidnightBlue, null, pupil, r * 0.105, r * 0.14);
            dc.DrawEllipse(System.Windows.Media.Brushes.White, null, new Point(pupil.X - r * 0.03, pupil.Y - r * 0.05), r * 0.035, r * 0.04);
        }
        dc.DrawLine(ink, new Point(-r * 0.27, -r * 0.86), new Point(r * 0.18, -r * (0.83 + anticipation * 0.09)));
        dc.DrawLine(shine, new Point(-r * 0.65, -r * 0.46), new Point(-r * 0.46, -r * 0.69));
        dc.DrawEllipse(System.Windows.Media.Brushes.DeepPink, null, new Point(-r * 0.12, r * 0.42), r * 0.15, r * 0.09);
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private void TurboDrawDigestingRibbon(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        var samples = _ribbonSamples;
        var left = new List<Point>(samples.Count);
        var right = new List<Point>(samples.Count);
        var distance = 0.0;
        var head = samples[0];
        var tail = samples[^1];
        var hue = (_hueOffset + (_rainbow ? t * 45 : 0)) % 360;
        for (int i = 1; i < samples.Count; i++)
        {
            distance += Len(samples[i - 1], samples[i]);
            if (distance < Cell * 0.58) continue;
            var before = samples[Math.Max(0, i - 3)];
            var after = samples[Math.Min(samples.Count - 1, i + 3)];
            var dx = after.X - before.X;
            var dy = after.Y - before.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001) continue;
            var color = hue;
            var radius = Cell * 0.39 * TurboDigestScale(distance, ref color);
            left.Add(new Point(samples[i].X - dy / length * radius, samples[i].Y + dx / length * radius));
            right.Add(new Point(samples[i].X + dy / length * radius, samples[i].Y - dx / length * radius));
        }
        if (left.Count < 2) return;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(left[0], true, true);
            for (int i = 1; i < left.Count; i++) ctx.LineTo(left[i], true, true);
            ctx.ArcTo(right[^1], new System.Windows.Size(Cell * 0.39, Cell * 0.39), 0, false, SweepDirection.Counterclockwise, true, true);
            for (int i = right.Count - 2; i >= 0; i--) ctx.LineTo(right[i], true, true);
            ctx.ArcTo(left[0], new System.Windows.Size(Cell * 0.39, Cell * 0.39), 0, false, SweepDirection.Counterclockwise, true, true);
        }
        geo.Freeze();
        if (Len(head, tail) < 0.01) tail = new Point(head.X + 1, head.Y);
        var fill = new LinearGradientBrush { StartPoint = head, EndPoint = tail, MappingMode = BrushMappingMode.Absolute };
        for (int i = 0; i < 6; i++)
        {
            var color = Hsl((hue + i * 55) % 360, 1, 0.62);
            fill.GradientStops.Add(new GradientStop(Color.FromArgb(i == 5 ? (byte)0 : (byte)255, color.R, color.G, color.B), i / 5.0));
        }
        fill.Freeze();
        if (Settings.DropShadow)
        {
            dc.PushOpacity(0.38);
            dc.DrawGeometry(canvas.GetFill((int)hue), canvas.GetStrokePen((int)hue, 9), geo);
            dc.Pop();
        }
        dc.DrawGeometry(fill, canvas.GetStrokePen((int)hue, 1.4), geo);
        TurboRibbonSheen(dc, left, right, head, tail, 0.22, Color.FromArgb(70, 255, 255, 255), 3.2);
        TurboRibbonSheen(dc, left, right, head, tail, 0.8, Color.FromArgb(45, 20, 0, 40), 5);
    }

    private static void TurboRibbonSheen(DrawingContext dc, List<Point> left, List<Point> right,
        Point head, Point tail, double blend, Color color, double width)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (int i = 0; i < left.Count; i++)
            {
                var point = new Point(left[i].X + (right[i].X - left[i].X) * blend,
                                      left[i].Y + (right[i].Y - left[i].Y) * blend);
                if (i == 0) ctx.BeginFigure(point, false, false);
                else ctx.LineTo(point, true, true);
            }
        }
        geo.Freeze();
        var brush = new LinearGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B), 0)
        {
            StartPoint = head,
            EndPoint = tail,
            MappingMode = BrushMappingMode.Absolute
        };
        brush.Freeze();
        var pen = new System.Windows.Media.Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }
}
