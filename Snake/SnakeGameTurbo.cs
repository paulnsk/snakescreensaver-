using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SnakeScreensaver.Snake;

public partial class SnakeGame
{
    internal static Color HslStatic(double h, double s, double l) => Hsl(h, s, l);
    internal static Geometry FoodGeometryStatic(int kind) => FoodGeometry(kind);

    private double _turboNow;
    private double _turboEatPopStart = -1;
    private const double TurboEatPopDur = 0.22;
    private readonly List<TurboParticle> _turboParticles = new();
    private readonly List<SnakeTurboCanvas> _turboViews = new();

    internal void SetTurboViewport(Rect bounds) => TurboCanvas.WorldBounds = bounds;

    internal SnakeTurboCanvas CreateTurboViewport(Rect bounds)
    {
        var canvas = new SnakeTurboCanvas
        {
            Host = this,
            WorldBounds = bounds,
            ViewportOffset = bounds.TopLeft,
            ClipToBounds = true
        };
        _turboViews.Add(canvas);
        return canvas;
    }

    private sealed class TurboParticle
    {
        public double X, Y, Vx, Vy, Born, Life, Size;
        public Color Color;
    }

    private void TurboInit() { }

    private void TurboResize() => TurboCanvas.InvalidateNow();

    private void TurboReset()
    {
        _turboParticles.Clear();
        _turboEatPopStart = -1;
        TurboResetFeeding();
        TurboResetPoop();
    }

    private Seg TurboCreateSeg() => new Seg { Half = Cell * 0.46 };

    private void TurboAddFood(Food f) => f.SpawnTime = Now;

    private void TurboEatFood(Food f)
    {
        _foods.Remove(f);
        var pos = CellCenter(f.Cell);
        if (Settings.MagicPoop) TurboQueuePoop(f);
        if (Settings.LivingSnake) TurboSwallow(f);
        else TurboBurst(pos, f.Hue, 28);
        _hueOffset = (_hueOffset + 33.0) % 360.0;
        _turboEatPopStart = Now;
    }

    private void TurboBurst(Point center, double hue, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var ph = (hue + _rng.Next(-30, 30) + 360) % 360.0;
            var l = _rng.NextDouble() * 0.2 + 0.55;
            var c = Hsl(ph, 1.0, l);
            var ang = _rng.NextDouble() * Math.PI * 2;
            var spd = _rng.Next(40, 170);
            _turboParticles.Add(new TurboParticle
            {
                X = center.X,
                Y = center.Y,
                Vx = Math.Cos(ang) * spd,
                Vy = Math.Sin(ang) * spd,
                Born = Now,
                Life = _rng.NextDouble() * 0.45 + 0.55,
                Size = _rng.Next(7, 16),
                Color = c
            });
        }
        if (_turboParticles.Count > 1200) _turboParticles.RemoveRange(0, _turboParticles.Count - 1200);
    }

    private void TurboRedraw(double t)
    {
        _turboNow = t;
        TurboUpdateFeeding(t);
        TurboUpdatePoop(t);
        TurboCanvas.InvalidateNow();
        foreach (var view in _turboViews) view.InvalidateNow();
    }

    internal void TurboRender(DrawingContext dc, SnakeTurboCanvas canvas)
    {
        var t = _turboNow;
        TurboDrawPoop(dc, canvas, t);
        TurboDrawFoods(dc, canvas, t);
        if (_mode == Settings.RenderMode.Balls) TurboDrawBalls(dc, canvas, t);
        else TurboDrawRibbon(dc, canvas, t);
        TurboDrawTailPortal(dc, canvas, t);
        if (Settings.LivingSnake)
        {
            TurboDrawSwallows(dc, canvas, t);
            TurboDrawFace(dc, canvas, t);
        }
        TurboDrawParticles(dc, canvas, t);
    }

    private void TurboDrawBalls(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        if (_segs.Count == 0) return;
        var radius = Cell * 0.46;

        var pop = 1.0;
        if (_turboEatPopStart > 0)
        {
            var pp = Math.Max(0, (t - _turboEatPopStart) / TurboEatPopDur);
            if (pp < 1) pop = 1 + 0.45 * Math.Sin(pp * Math.PI);
            else _turboEatPopStart = -1;
        }

        if (Settings.DropShadow)
        {
            for (int i = _segs.Count - 1; i >= 0; i--)
            {
                if (Settings.LivingSnake && i == 0) continue;
                var seg = _segs[i];
                var progress = seg.MoveDuration > 0 ? Math.Clamp((t - seg.MoveStart) / seg.MoveDuration, 0, 1) : 1;
                var curX = seg.Stored.X + (seg.To.X - seg.Stored.X) * progress;
                var curY = seg.Stored.Y + (seg.To.Y - seg.Stored.Y) * progress;
                var hue = (_hueOffset + (_rainbow ? t * 55.0 : 0) + i * 11.0) % 360.0;
                var swell = TurboDigestScale(i * Cell, ref hue);
                var r = radius * (i == 0 ? pop : swell);
                var halo = r * 1.45;
                if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(curX + 4 - halo, curY + 6 - halo, halo * 2, halo * 2))) continue;
                var glow = canvas.GetGlowImage((int)hue, radius * 1.45);
                var size = glow.Width * r / radius;
                dc.DrawImage(glow, new Rect(curX + 4 - size / 2, curY + 6 - size / 2, size, size));
            }
        }

        for (int i = _segs.Count - 1; i >= 0; i--)
        {
            if (Settings.LivingSnake && i == 0) continue;
            var seg = _segs[i];
            var progress = seg.MoveDuration > 0 ? Math.Clamp((t - seg.MoveStart) / seg.MoveDuration, 0, 1) : 1;
            var curX = seg.Stored.X + (seg.To.X - seg.Stored.X) * progress;
            var curY = seg.Stored.Y + (seg.To.Y - seg.Stored.Y) * progress;
            var hue = (_hueOffset + (_rainbow ? t * 55.0 : 0) + i * 11.0) % 360.0;
            var swell = TurboDigestScale(i * Cell, ref hue);
            var r = radius * (i == 0 ? pop : swell);
            if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(curX - r - 1, curY - r - 1, r * 2 + 2, r * 2 + 2))) continue;
            var hueKey = (int)hue;
            var fill = canvas.GetFillImage(hueKey, radius);
            var size = fill.Width * r / radius;
            dc.DrawImage(fill, new Rect(curX - size / 2, curY - size / 2, size, size));
        }
    }

    private void TurboDrawFoods(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        if (_foods.Count == 0) return;
        var w = Cell * 0.78;

        foreach (var f in _foods)
        {
            var pos = CellCenter(f.Cell);
            var age = Math.Max(0, t - f.SpawnTime);
            var pulse = 1.0 + 0.1 * Math.Sin(age * f.PulseFreq);
            var rot = age * f.RotSpeed * 360;
            var halfW = w * 0.5 * pulse;
            if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(pos.X - halfW * 2, pos.Y - halfW * 2, halfW * 4, halfW * 4))) continue;
            var hueKey = (int)f.Hue;

            if (Settings.DropShadow)
            {
                var glow = canvas.GetGlow(hueKey);
                var halo = halfW * 1.4;
                dc.DrawEllipse(glow, null, new Point(pos.X + 2, pos.Y + 4), halo, halo);
            }

            var fill = canvas.GetFill(hueKey);
            var strokePen = canvas.GetStrokePen((int)((f.Hue + 320) % 360.0), 1.8);

            if (f.Kind == 0)
            {
                dc.DrawEllipse(fill, strokePen, new Point(pos.X, pos.Y), halfW, halfW);
            }
            else
            {
                var geo = canvas.GetFoodGeometry(f.Kind);
                var scaleFactor = (halfW * 2.0) / 17.5;
                dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
                dc.PushTransform(new RotateTransform(rot));
                dc.PushTransform(new ScaleTransform(scaleFactor, scaleFactor));
                dc.PushTransform(new TranslateTransform(-9, -9));
                dc.DrawGeometry(fill, strokePen, geo);
                dc.Pop();
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
        }
    }

    private void TurboDrawRibbon(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        if (_ribbonSamples.Count < 2 || !_ribbonReady) return;
        if (Settings.LivingSnake)
        {
            TurboDrawDigestingRibbon(dc, canvas, t);
            return;
        }
        var head = _ribbonSamples[0];
        var tail = _ribbonSamples[^1];
        if (Len(head, tail) < 0.001) tail = new Point(head.X + 1, head.Y);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(head, false, false);
            for (int i = 1; i < _ribbonSamples.Count; i++)
                ctx.LineTo(_ribbonSamples[i], true, true);
        }
        geo.Freeze();

        var brush = new LinearGradientBrush
        {
            StartPoint = head,
            EndPoint = tail,
            MappingMode = BrushMappingMode.Absolute
        };
        for (int i = 0; i < 6; i++)
        {
            var hue = (_hueOffset + (_rainbow ? t * 45.0 : 0) + i * 55.0) % 360.0;
            var c = Hsl(hue, 1.0, 0.62);
            var alpha = i == 5 ? (byte)0 : (byte)255;
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, c.R, c.G, c.B), i / 5.0));
        }
        brush.Freeze();

        var strokeWidth = Cell * 0.78;

        if (Settings.DropShadow)
        {
            var haloHue = (_hueOffset + (_rainbow ? t * 60.0 : 0)) % 360.0;
            var haloC = Hsl(haloHue, 1.0, 0.55);
            var haloBrush = new SolidColorBrush(Color.FromArgb(150, haloC.R, haloC.G, haloC.B));
            haloBrush.Freeze();
            var haloPen = new System.Windows.Media.Pen(haloBrush, strokeWidth + 8)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            haloPen.Freeze();
            dc.DrawGeometry(null, haloPen, geo);
        }

        var pen = new System.Windows.Media.Pen(brush, strokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private void TurboDrawParticles(DrawingContext dc, SnakeTurboCanvas canvas, double t)
    {
        if (_turboParticles.Count == 0) return;
        for (int i = _turboParticles.Count - 1; i >= 0; i--)
        {
            var p = _turboParticles[i];
            var age = Math.Max(0, t - p.Born);
            if (age >= p.Life) { _turboParticles.RemoveAt(i); continue; }
            var eased = 1 - Math.Pow(1 - age / p.Life, 2);
            var x = p.X + p.Vx * eased * 0.5;
            var y = p.Y + p.Vy * eased * 0.5 - 20 * age * age;
            var alpha = 1 - age / p.Life;
            var size = p.Size * (1 - 0.4 * age / p.Life);
            if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(x - size, y - size, size * 2, size * 2))) continue;
            dc.PushOpacity(alpha);
            var brush = new SolidColorBrush(p.Color);
            brush.Freeze();
            dc.DrawEllipse(brush, null, new Point(x, y), size * 0.5, size * 0.5);
            dc.Pop();
        }
    }
}
