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
    private const double BurpDelay = 0.3;
    private const double BurpLife = 1.3;
    private double _faceAnger, _tongue, _lidTwitch, _nostrilFlare;
    private double _twitchStart, _nextTwitch, _flickStart, _nextFlick, _snortStart, _nextSnort;
    private readonly List<BurpStar> _burpStars = new();
    private static readonly Geometry BurpStarGeometry = CreateBurpStarGeometry();

    private sealed class Meal
    {
        public Food Food = null!;
        public Point Origin;
        public double Born, Age, Center, Amplitude, Width;
        public bool Burped;
    }

    private sealed class BurpStar
    {
        public double X, Y, Vx, Vy, Born, Spin, Size;
        public int Hue;
    }

    private static Geometry CreateBurpStarGeometry()
    {
        var star = new StreamGeometry();
        using (var ctx = star.Open())
        {
            for (int i = 0; i < 10; i++)
            {
                var a = -Math.PI / 2 + i * Math.PI / 5;
                var k = i % 2 == 0 ? 1 : 0.42;
                var p = new Point(Math.Cos(a) * k, Math.Sin(a) * k);
                if (i == 0) ctx.BeginFigure(p, true, true);
                else ctx.LineTo(p, true, true);
            }
        }
        star.Freeze();
        return star;
    }

    private void TurboResetFeeding()
    {
        _meals.Clear();
        _faceAngle = 0;
        _faceLastTime = Now;
        _jawAngle = 0;
        _headRadius = Cell * 0.78;
        _faceGaze = new Point(1, 0);
        _burpStars.Clear();
        _faceAnger = 0.3;
        _tongue = 0;
        _lidTwitch = 0;
        _nostrilFlare = 0;
        _twitchStart = _flickStart = _snortStart = -10;
        _nextTwitch = Now + 2;
        _nextFlick = Now + 1.2;
        _nextSnort = Now + 1.5;
    }

    private Point TurboFaceToWorld(Point origin, Point local, double bob)
    {
        var flip = Math.Cos(_faceAngle) < 0 ? -1 : 1;
        var x = local.X * Math.Cos(bob) - local.Y * Math.Sin(bob);
        var y = (local.X * Math.Sin(bob) + local.Y * Math.Cos(bob)) * flip;
        var cos = Math.Cos(_faceAngle);
        var sin = Math.Sin(_faceAngle);
        return new Point(origin.X + x * cos - y * sin, origin.Y + x * sin + y * cos);
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
            _burpStars.Clear();
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
        var swallowing = 0.0;
        List<Meal>? burps = null;
        foreach (var meal in _meals)
        {
            var burp = meal.Age - SwallowTime - BurpDelay;
            if (burp >= 0 && !meal.Burped)
            {
                meal.Burped = true;
                (burps ??= new List<Meal>()).Add(meal);
            }
            if (burp >= -0.2 && burp < 0.3)
            {
                opening = Math.Max(opening, burp < 0 ? 0 : Math.Sin(burp / 0.3 * Math.PI) * 0.9);
                swell = Math.Max(swell, burp < 0 ? (burp + 0.2) / 0.2 * 0.16 : 0.16 * (1 - burp / 0.3));
            }
            var progress = meal.Age / SwallowTime;
            if (progress >= 1.5) continue;
            var gape = progress < 0.65 ? 1 : Math.Clamp((1 - progress) / 0.35, 0, 1);
            opening = Math.Max(opening, gape);
            swallowing = Math.Max(swallowing, gape);
            swell = Math.Max(swell, (0.25 + meal.Food.Growth * 0.07)
                * (progress < 0.65 ? 1 : Math.Max(0, 1 - (progress - 0.65) / 0.85)));
        }
        _jawAngle = opening * 52 * Math.PI / 180;
        _headRadius = Cell * 0.78 * (1 + swell);

        _faceAnger += (0.3 + 0.7 * Math.Max(anticipation, swallowing) - _faceAnger) * (1 - Math.Exp(-dt * 3));
        if (t >= _nextTwitch)
        {
            _twitchStart = t;
            _nextTwitch = t + 2.5 + _rng.NextDouble() * 3;
        }
        var twitch = t - _twitchStart;
        _lidTwitch = twitch < 0.32 ? Math.Abs(Math.Sin(twitch / 0.32 * 2 * Math.PI)) : 0;
        if (t >= _nextFlick)
        {
            _flickStart = t;
            _nextFlick = t + 2.8 + _rng.NextDouble() * 2.5;
        }
        var flick = t - _flickStart;
        var idleTongue = flick < 0.5 ? Math.Sin(flick / 0.5 * Math.PI) * (0.8 + 0.2 * Math.Sin(flick * 70)) : 0;
        var huntTongue = anticipation * (0.55 + 0.45 * Math.Abs(Math.Sin(t * 13)));
        _tongue = Math.Max(idleTongue, huntTongue) * (1 - swallowing);

        var r = _headRadius;
        if (t >= _nextSnort)
        {
            _snortStart = t;
            _nextSnort = t + 2.8 - 1.9 * _faceAnger + _rng.NextDouble() * 0.7;
            var nostril = TurboFaceToWorld(head, new Point(r * 0.8, -r * 0.2), -_jawAngle);
            for (int i = 0; i < 5; i++)
            {
                var dir = TurboFaceToWorld(new Point(), new Point(0.85, -0.5 + (_rng.NextDouble() - 0.5) * 0.7), -_jawAngle);
                var speed = 50 + _rng.NextDouble() * 60;
                var grey = (byte)_rng.Next(225, 255);
                _turboParticles.Add(new TurboParticle
                {
                    X = nostril.X,
                    Y = nostril.Y,
                    Vx = dir.X * speed,
                    Vy = dir.Y * speed,
                    Born = t,
                    Life = 0.55 + _rng.NextDouble() * 0.35,
                    Size = r * (0.5 + _rng.NextDouble() * 0.35),
                    Color = Color.FromRgb(grey, grey, 255)
                });
            }
        }
        _nostrilFlare = Math.Exp(-Math.Max(0, t - _snortStart) * 6);

        foreach (var meal in burps ?? (IEnumerable<Meal>)Array.Empty<Meal>())
        {
            var mouth = TurboFaceToWorld(head, new Point(r * 1.05, 0), 0);
            for (int i = 0; i < 7; i++)
            {
                var forward = 40 + _rng.NextDouble() * 90;
                var side = (_rng.NextDouble() - 0.5) * 90;
                _burpStars.Add(new BurpStar
                {
                    X = mouth.X,
                    Y = mouth.Y,
                    Vx = cos * forward - sin * side,
                    Vy = sin * forward + cos * side - 50 - _rng.NextDouble() * 70,
                    Born = t,
                    Spin = (_rng.NextDouble() - 0.5) * 9,
                    Size = r * (0.16 + _rng.NextDouble() * 0.14),
                    Hue = ((int)meal.Food.Hue + _rng.Next(-40, 40) + 360) % 360
                });
            }
        }
        _burpStars.RemoveAll(s => t - s.Born >= BurpLife);
    }

    private void TurboDrawBurps(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        foreach (var star in _burpStars)
        {
            var age = Math.Max(0, t - star.Born);
            var k = age / BurpLife;
            var pos = new Point(star.X + star.Vx * age, star.Y + star.Vy * age + 35 * age * age);
            var size = star.Size * Math.Min(1, k / 0.1) * (1 - k * 0.45);
            if (size <= 0.01) continue;
            if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(pos.X - size, pos.Y - size, size * 2, size * 2))) continue;
            var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 1.4 / size);
            pen.Freeze();
            dc.PushOpacity(1 - k * k);
            dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
            dc.PushTransform(new RotateTransform(star.Spin * age * 180 / Math.PI));
            dc.PushTransform(new ScaleTransform(size, size));
            dc.DrawGeometry(canvas.GetFill(star.Hue), pen, BurpStarGeometry);
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
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
        if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(head.X - r * 3.5, head.Y - r * 3.5, r * 7, r * 7))) return;
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
        var bobDeg = -_jawAngle * 0.22 * 180 / Math.PI;
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
        var anger = _faceAnger;
        var fang = Math.Min(r * 0.3, r * Math.Sin(angle) * 0.8);

        if (_tongue > 0.02)
        {
            var baseX = r * (0.2 + 0.7 * Math.Clamp(1 - angle / 0.5, 0, 1));
            var tipX = Math.Max(baseX, r * 0.75) + r * 0.95 * _tongue;
            var wiggle = Math.Sin(t * 42) * r * 0.09 * _tongue;
            var tip = new Point(tipX, -wiggle * 0.5);
            var prong = r * 0.22 * _tongue;
            var tongue = new StreamGeometry();
            using (var ctx = tongue.Open())
            {
                ctx.BeginFigure(new Point(baseX, 0), false, false);
                ctx.QuadraticBezierTo(new Point((baseX + tipX) / 2, wiggle * 2), tip, true, true);
                ctx.BeginFigure(new Point(tipX + prong, tip.Y - prong * 0.5), false, false);
                ctx.LineTo(tip, true, true);
                ctx.LineTo(new Point(tipX + prong, tip.Y + prong * 0.5), true, true);
            }
            tongue.Freeze();
            var edge = new System.Windows.Media.Pen(new SolidColorBrush(Color.FromRgb(110, 0, 35)), r * 0.13)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            edge.Freeze();
            var core = new System.Windows.Media.Pen(new SolidColorBrush(Color.FromRgb(240, 48, 106)), r * 0.075)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            core.Freeze();
            dc.DrawGeometry(null, edge, tongue);
            dc.DrawGeometry(null, core, tongue);
        }

        for (int jaw = -1; angle > 0.12 && jaw <= 1; jaw += 2)
        {
            dc.PushTransform(new RotateTransform(jaw * angle * 180 / Math.PI));
            var teeth = new StreamGeometry();
            using (var ctx = teeth.Open())
            {
                ctx.BeginFigure(new Point(r * 0.48, 0), true, true);
                ctx.LineTo(new Point(r * 0.68, 0), true, false);
                ctx.LineTo(new Point(r * 0.58, -jaw * fang), true, false);
                ctx.BeginFigure(new Point(r * 0.74, 0), true, true);
                ctx.LineTo(new Point(r * 0.88, 0), true, false);
                ctx.LineTo(new Point(r * 0.81, -jaw * fang * 0.5), true, false);
            }
            teeth.Freeze();
            dc.DrawGeometry(System.Windows.Media.Brushes.Ivory, ink, teeth);
            dc.Pop();
        }

        var drool = t % 2.4 / 2.4;
        var lip = r * (0.58 + 0.42 * Math.Clamp(1 - angle / 0.35, 0, 1));
        var anchor = new Point(lip * Math.Cos(angle) + fang * Math.Sin(angle),
            -lip * Math.Sin(angle) + fang * Math.Cos(angle));
        var down = new Point(sin, cos * flip);
        var slime = new SolidColorBrush(Color.FromArgb(200, 200, 240, 255));
        slime.Freeze();
        var slimePen = new System.Windows.Media.Pen(slime, r * 0.05)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        slimePen.Freeze();
        var stretch = Math.Min(1, drool / 0.82);
        var dropRadius = r * (0.05 + 0.05 * stretch);
        Point drop;
        var dropAlpha = 1.0;
        if (drool < 0.82)
        {
            var hang = r * (0.1 + 0.6 * Math.Pow(stretch, 1.5));
            drop = new Point(anchor.X + down.X * hang, anchor.Y + down.Y * hang);
            dc.DrawLine(slimePen, anchor, drop);
        }
        else
        {
            var fall = (drool - 0.82) / 0.18;
            var stub = r * 0.15 * (1 - fall);
            dc.DrawLine(slimePen, anchor, new Point(anchor.X + down.X * stub, anchor.Y + down.Y * stub));
            var distance = r * (0.7 + fall * fall * 2.6);
            drop = new Point(anchor.X + down.X * distance, anchor.Y + down.Y * distance);
            dropAlpha = 1 - fall;
        }
        dc.PushOpacity(dropAlpha);
        dc.DrawEllipse(slime, null, drop, dropRadius, dropRadius);
        dc.DrawEllipse(System.Windows.Media.Brushes.White, null, new Point(drop.X - dropRadius * 0.35, drop.Y - dropRadius * 0.35), dropRadius * 0.3, dropRadius * 0.3);
        dc.Pop();

        var flare = 1 + 0.25 * anger + 0.7 * _nostrilFlare;
        dc.PushTransform(new RotateTransform(-angle * 180 / Math.PI));
        dc.DrawEllipse(System.Windows.Media.Brushes.MidnightBlue, null, new Point(r * 0.8, -r * 0.2), r * 0.07 * flare, r * 0.04 * flare);
        dc.DrawEllipse(System.Windows.Media.Brushes.MidnightBlue, null, new Point(r * 0.66, -r * 0.25), r * 0.045 * flare, r * 0.03 * flare);
        dc.Pop();

        dc.PushTransform(new RotateTransform(bobDeg));

        var eye = new Point(r * 0.08, -r * 0.48);
        var eyeX = r * 0.29;
        var eyeY = r * 0.26;
        dc.DrawEllipse(System.Windows.Media.Brushes.Ivory, ink, eye, eyeX, eyeY);
        var vein = new System.Windows.Media.Pen(new SolidColorBrush(Color.FromRgb(220, 30, 50)), Math.Max(1, r * 0.028))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        vein.Freeze();
        dc.PushOpacity(0.25 + 0.7 * anger);
        foreach (var a in VeinAngles)
        {
            var rim = new Point(eye.X + eyeX * 0.95 * Math.Cos(a), eye.Y + eyeY * 0.95 * Math.Sin(a));
            var mid = new Point(eye.X + eyeX * 0.74 * Math.Cos(a + 0.18), eye.Y + eyeY * 0.74 * Math.Sin(a + 0.18));
            var end = new Point(eye.X + eyeX * 0.56 * Math.Cos(a - 0.08), eye.Y + eyeY * 0.56 * Math.Sin(a - 0.08));
            dc.DrawLine(vein, rim, mid);
            dc.DrawLine(vein, mid, end);
        }
        dc.Pop();
        var length = Math.Max(1, Math.Sqrt(gaze.X * gaze.X + gaze.Y * gaze.Y));
        var iris = new Point(eye.X + (gaze.X * cos + gaze.Y * sin) / length * r * 0.1,
            eye.Y + (-gaze.X * sin + gaze.Y * cos) / length * r * 0.07 * flip);
        dc.DrawEllipse(System.Windows.Media.Brushes.Gold, null, iris, r * 0.16, r * 0.16);
        dc.DrawEllipse(System.Windows.Media.Brushes.MidnightBlue, null, iris, r * Math.Max(0.025, 0.065 - 0.035 * anger), r * 0.13);
        dc.DrawEllipse(System.Windows.Media.Brushes.White, null, new Point(iris.X - r * 0.05, iris.Y - r * 0.07), r * 0.035, r * 0.045);
        var lidDrop = r * (0.06 * anger + 0.2 * _lidTwitch);
        var lidBack = new Point(-r * 0.36, -r * 0.8 + lidDrop);
        var lidFront = new Point(r * 0.48, -r * 0.4 + lidDrop);
        var lid = new StreamGeometry();
        using (var ctx = lid.Open())
        {
            ctx.BeginFigure(new Point(-r * 0.36, -r * 0.9), true, true);
            ctx.LineTo(new Point(r * 0.48, -r * 0.9), true, false);
            ctx.LineTo(lidFront, true, false);
            ctx.LineTo(lidBack, true, false);
        }
        lid.Freeze();
        var eyeClip = new EllipseGeometry(eye, eyeX, eyeY);
        eyeClip.Freeze();
        dc.PushClip(eyeClip);
        dc.DrawGeometry(canvas.GetFill(hue), null, lid);
        dc.DrawLine(ink, lidBack, lidFront);
        dc.Pop();

        var frown = r * (0.08 * anger + 0.04 * _lidTwitch);
        var browTail = new Point(-r * 0.26, -r * 0.82 - frown * 0.5);
        var browTop = new Point(r * 0.02, -r * 0.98 - frown * 0.5);
        var browCrest = new Point(r * 0.44, -r * 0.66 + frown);
        var browBottom = new Point(r * 0.4, -r * 0.5 + frown);
        var brow = new StreamGeometry();
        using (var ctx = brow.Open())
        {
            ctx.BeginFigure(browTail, true, true);
            ctx.QuadraticBezierTo(browTop, browCrest, true, true);
            ctx.LineTo(browBottom, true, true);
            ctx.QuadraticBezierTo(new Point(0, -r * 0.8), browTail, true, true);
        }
        brow.Freeze();
        var spikes = new StreamGeometry();
        using (var ctx = spikes.Open())
        {
            for (int i = 1; i <= 4; i++)
            {
                var k = i * 0.2;
                var at = new Point((1 - k) * (1 - k) * browTail.X + 2 * (1 - k) * k * browTop.X + k * k * browCrest.X,
                    (1 - k) * (1 - k) * browTail.Y + 2 * (1 - k) * k * browTop.Y + k * k * browCrest.Y);
                var tall = r * (0.13 - k * 0.06);
                ctx.BeginFigure(new Point(at.X - r * 0.06, at.Y + r * 0.03), true, true);
                ctx.LineTo(new Point(at.X - r * 0.07, at.Y - tall), true, true);
                ctx.LineTo(new Point(at.X + r * 0.05, at.Y + r * 0.03), true, true);
            }
        }
        spikes.Freeze();
        var browBrush = new SolidColorBrush(Color.FromRgb(40, 20, 60));
        browBrush.Freeze();
        dc.DrawGeometry(browBrush, null, spikes);
        dc.DrawGeometry(browBrush, null, brow);
        dc.DrawLine(shine, new Point(-r * 0.65, -r * 0.46), new Point(-r * 0.46, -r * 0.69));
        dc.Pop();
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private static readonly double[] VeinAngles = { 2.5, 3.0, 3.5, 0.6 };
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
