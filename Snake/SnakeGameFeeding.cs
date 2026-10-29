using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SnakeScreensaver.Snake;

public partial class SnakeGame
{
    private const double SwallowTime = 0.46;
    private const double DigestTime = 4.0;
    private readonly List<Meal> _meals = new();
    private double _faceAngle;
    private double _faceLastTime;
    private double _jawAngle;
    private double _headRadius;
    private Point _faceGaze;

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
        _jawAngle = 0;
        _headRadius = Cell * 0.78;
        _faceGaze = new Point(1, 0);
    }

    private void TurboSwallow(Food food)
    {
        var born = _mode == Settings.RenderMode.Balls ? _lastStepTime : Now;
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
            meal.Center = TurboDigestionDistance(age);
            meal.Amplitude = Math.Clamp(age / 0.25, 0, 1) * Math.Pow(1 - progress, 1.4)
                * (0.55 + Math.Min(0.35, meal.Food.Growth * 0.07));
        }
        var head = TurboHeadPosition(t);
        var cos = Math.Cos(_faceAngle);
        var sin = Math.Sin(_faceAngle);
        var nearest = double.MaxValue;
        var anticipation = 0.0;
        _faceGaze = new Point(cos, sin);
        foreach (var food in _foods)
        {
            var pos = CellCenter(food.Cell);
            var distance = Len(head, pos);
            if (distance >= nearest) continue;
            nearest = distance;
            _faceGaze = new Point(pos.X - head.X, pos.Y - head.Y);
        }
        if (_faceGaze.X * cos + _faceGaze.Y * sin > 0)
            anticipation = Math.Clamp(1 - (nearest - Cell * 0.7) / (Cell * 1.8), 0, 1);
        var phase = t * 3.1 % 1;
        var chomp = phase < 0.1 || phase >= 0.94 ? 0
            : phase < 0.7 ? Math.Sin((phase - 0.1) / 0.6 * Math.PI / 2)
            : Math.Pow((0.94 - phase) / 0.24, 2);
        var opening = chomp * (1 - anticipation) + anticipation;
        var swell = anticipation * 0.18;
        foreach (var meal in _meals)
        {
            var progress = meal.Age / SwallowTime;
            if (progress >= 1.5) continue;
            var gape = progress < 0.65 ? 1 : Math.Clamp((1 - progress) / 0.35, 0, 1);
            opening = Math.Max(opening, gape);
            swell = Math.Max(swell, (0.25 + meal.Food.Growth * 0.07)
                * (progress < 0.65 ? 1 : Math.Max(0, 1 - (progress - 0.65) / 0.85)));
        }
        _jawAngle = opening * 52 * Math.PI / 180;
        _headRadius = Cell * 0.78 * (1 + swell);
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
        var cos = Math.Cos(_faceAngle);
        var sin = Math.Sin(_faceAngle);
        foreach (var meal in _meals)
        {
            if (meal.Age >= SwallowTime) continue;
            var progress = Math.Clamp(meal.Age / SwallowTime, 0, 1);
            var pull = progress * progress * (3 - 2 * progress);
            var dx = meal.Origin.X - head.X;
            var dy = meal.Origin.Y - head.Y;
            var forward = Math.Max(_headRadius * 1.05, dx * cos + dy * sin);
            var side = (-dx * sin + dy * cos) * (1 - pull);
            var along = forward * (1 - pull) - _headRadius * 0.35 * pull;
            var pos = new Point(head.X + cos * along - sin * side,
                                head.Y + sin * along + cos * side);
            var size = Cell * 0.78 * (1 - Math.Max(0, progress - 0.65) * 1.4);
            if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(pos.X - size, pos.Y - size, size * 2, size * 2))) continue;
            if (Settings.DropShadow)
                dc.DrawEllipse(canvas.GetGlow((int)meal.Food.Hue), null, pos, size * 0.8, size * 0.8);
            dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
            dc.PushTransform(new RotateTransform(t * meal.Food.RotSpeed * 360 + progress * 90));
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
        }
    }

    private void TurboDrawFace(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        var head = TurboHeadPosition(t);
        var r = _headRadius;
        if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(head.X - r * 2, head.Y - r * 2, r * 4, r * 4))) return;
        var cos = Math.Cos(_faceAngle);
        var sin = Math.Sin(_faceAngle);
        var gaze = _faceGaze;
        var angle = Math.Max(0.001, _jawAngle);
        var hue = (int)((_hueOffset + (_rainbow ? t * 55 : 0)) % 360);

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
            ctx.BeginFigure(new Point(), true, true);
            ctx.LineTo(upper, true, false);
            ctx.ArcTo(lower, new System.Windows.Size(r, r), 0, true, SweepDirection.Counterclockwise, true, true);
        }
        face.Freeze();
        dc.DrawGeometry(canvas.GetFill(hue), canvas.GetStrokePen(hue, 1.8), face);

        var ink = new System.Windows.Media.Pen(System.Windows.Media.Brushes.MidnightBlue, 2.1)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        ink.Freeze();
        var shine = new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 1.7);
        shine.Freeze();
        for (int jaw = -1; angle > 0.12 && jaw <= 1; jaw += 2)
        {
            dc.PushTransform(new RotateTransform(jaw * angle * 180 / Math.PI));
            var teeth = new StreamGeometry();
            using (var ctx = teeth.Open())
            {
                ctx.BeginFigure(new Point(r * 0.55, 0), true, true);
                ctx.LineTo(new Point(r * 0.76, 0), true, false);
                ctx.LineTo(new Point(r * 0.64, -jaw * Math.Min(r * 0.22, r * Math.Sin(angle) * 0.5)), true, false);
            }
            teeth.Freeze();
            dc.DrawGeometry(System.Windows.Media.Brushes.Ivory, ink, teeth);
            dc.Pop();
        }
        var eye = new Point(-r * 0.16, -r * 0.53);
        dc.DrawEllipse(System.Windows.Media.Brushes.Ivory, ink, eye, r * 0.3, r * 0.25);
        var length = Math.Max(1, Math.Sqrt(gaze.X * gaze.X + gaze.Y * gaze.Y));
        var pupil = new Point(eye.X + (gaze.X * cos + gaze.Y * sin) / length * r * 0.12,
            eye.Y + (-gaze.X * sin + gaze.Y * cos) / length * r * 0.07 * flip);
        dc.DrawEllipse(System.Windows.Media.Brushes.MidnightBlue, null, pupil, r * 0.095, r * 0.16);
        dc.DrawEllipse(System.Windows.Media.Brushes.White, null, new Point(pupil.X - r * 0.025, pupil.Y - r * 0.06), r * 0.03, r * 0.04);
        var lid = new StreamGeometry();
        using (var ctx = lid.Open())
        {
            ctx.BeginFigure(new Point(-r * 0.49, -r * 0.87), true, true);
            ctx.LineTo(new Point(r * 0.18, -r * 0.87), true, false);
            ctx.LineTo(new Point(r * 0.18, -r * 0.43), true, false);
            ctx.LineTo(new Point(-r * 0.49, -r * 0.72), true, false);
        }
        lid.Freeze();
        dc.DrawGeometry(canvas.GetFill(hue), null, lid);
        dc.DrawLine(ink, new Point(-r * 0.49, -r * 0.72), new Point(r * 0.18, -r * 0.43));
        dc.DrawLine(ink, new Point(-r * 0.62, r * 0.14), new Point(-r * 0.42, r * 0.29));
        dc.DrawLine(ink, new Point(-r * 0.42, r * 0.29), new Point(-r * 0.17, r * 0.17));
        dc.DrawLine(shine, new Point(-r * 0.65, -r * 0.46), new Point(-r * 0.46, -r * 0.69));
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
