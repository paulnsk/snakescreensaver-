using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace SnakeScreensaver.Snake;

public partial class SnakeGame
{
    private const int SteelNut = -1;
    private const int HazardNut = -2;
    private const int ShadowNut = -3;
    private static readonly Dictionary<int, BitmapSource> NutImages = new();
    private static readonly Color HazardYellow = Color.FromRgb(0xFF, 0xD6, 0x0A);
    private static readonly SolidColorBrush InkBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x0B, 0x0B, 0x0D)));
    private static readonly SolidColorBrush CableBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x4A, 0x4F, 0x59)));
    private static readonly SolidColorBrush ShadowBrush = Frozen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)));
    private static readonly SolidColorBrush LightSteelBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xC9, 0xCE, 0xD6)));
    private static readonly SolidColorBrush AcidBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xB6, 0xFF, 0x3B)));
    private static readonly SolidColorBrush BloodBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x3B)));
    private static readonly SolidColorBrush SparkBrush = Frozen(new SolidColorBrush(HazardYellow));
    private static readonly System.Windows.Media.Pen InkPen = Frozen(new System.Windows.Media.Pen(InkBrush, 2) { LineJoin = PenLineJoin.Round });
    private static readonly System.Windows.Media.Pen ThinInkPen = Frozen(new System.Windows.Media.Pen(InkBrush, 1.2));
    private static readonly System.Windows.Media.Pen SparkPen = Frozen(new System.Windows.Media.Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x1A)), 1.8));
    private static readonly Brush SteelBrush = Frozen(new LinearGradientBrush(Color.FromRgb(0xE3, 0xE6, 0xEA), Color.FromRgb(0x6F, 0x76, 0x82), 45));
    private static readonly Brush SteelHeadBrush = Frozen(new RadialGradientBrush(Color.FromRgb(0xD9, 0xDD, 0xE3), Color.FromRgb(0x5E, 0x65, 0x70))
    {
        GradientOrigin = new Point(0.35, 0.3)
    });
    private static readonly Geometry HexGeometry = CreateHexGeometry();

    private static bool Brutal => Settings.Style == Settings.SnakeStyle.Brutal;

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static Geometry CreateHexGeometry()
    {
        var hex = new StreamGeometry();
        using (var ctx = hex.Open())
        {
            for (int i = 0; i < 6; i++)
            {
                var a = i * Math.PI / 3;
                var p = new Point(Math.Cos(a), Math.Sin(a));
                if (i == 0) ctx.BeginFigure(p, true, true);
                else ctx.LineTo(p, true, true);
            }
        }
        hex.Freeze();
        return hex;
    }

    private static BitmapSource GetNutImage(int key, double radius)
    {
        if (NutImages.TryGetValue(key, out var image)) return image;
        var size = (int)Math.Ceiling(radius * 2 + 6);
        var c = size / 2.0;
        Brush body = key switch
        {
            HazardNut => new LinearGradientBrush(Color.FromRgb(0xFF, 0xE4, 0x5C), Color.FromRgb(0xE0, 0xA8, 0x00), 45),
            ShadowNut => new SolidColorBrush(Color.FromArgb(115, 0, 0, 0)),
            _ => SteelBrush
        };
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(c, c));
            dc.PushTransform(new ScaleTransform(radius, radius));
            dc.DrawGeometry(body, null, HexGeometry);
            dc.Pop();
            dc.Pop();
            if (key != ShadowNut)
            {
                var hex = HexGeometry.Clone();
                hex.Transform = new TransformGroup { Children = { new ScaleTransform(radius, radius), new TranslateTransform(c, c) } };
                dc.DrawGeometry(null, InkPen, hex);
                var chamfer = new System.Windows.Media.Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 1.2);
                dc.DrawEllipse(null, chamfer, new Point(c, c), radius * 0.8, radius * 0.8);
                dc.DrawEllipse(InkBrush, new System.Windows.Media.Pen(new SolidColorBrush(Color.FromRgb(0x8E, 0x95, 0xA0)), 1.4),
                    new Point(c, c), radius * 0.34, radius * 0.34);
            }
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        NutImages[key] = bitmap;
        return bitmap;
    }

    private void TurboDrawBrutalBalls(DrawingContext dc, SnakeTurboCanvas canvas, double t, double pop)
    {
        var radius = Cell * 0.5;
        for (int pass = Settings.DropShadow ? 0 : 1; pass < 2; pass++)
        {
            for (int i = _segs.Count - 1; i >= 0; i--)
            {
                if (Settings.LivingSnake && i == 0) continue;
                var seg = _segs[i];
                var progress = seg.MoveDuration > 0 ? Math.Clamp((t - seg.MoveStart) / seg.MoveDuration, 0, 1) : 1;
                var x = seg.Stored.X + (seg.To.X - seg.Stored.X) * progress;
                var y = seg.Stored.Y + (seg.To.Y - seg.Stored.Y) * progress;
                var hue = (_hueOffset + (_rainbow ? t * 55.0 : 0) + i * 11.0) % 360.0;
                var r = radius * (i == 0 ? pop : TurboDigestScale(i * Cell, ref hue));
                if (pass == 0) { x += 4; y += 5; }
                if (!canvas.WorldBounds.IsEmpty && !canvas.WorldBounds.IntersectsWith(new Rect(x - r - 2, y - r - 2, r * 2 + 4, r * 2 + 4))) continue;
                var key = pass == 0 ? ShadowNut : i % 5 == 4 ? HazardNut : SteelNut;
                var nut = GetNutImage(key, radius);
                var size = nut.Width * r / radius;
                dc.PushTransform(new RotateTransform(i * 20 + t * 30, x, y));
                dc.DrawImage(nut, new Rect(x - size / 2, y - size / 2, size, size));
                dc.Pop();
            }
        }
    }

    private const double ClampPeriod = Cell * 2.5;
    private const double ClampLength = Cell * 0.42;
    private const double ClampStart = Cell * 1.3;

    private static System.Windows.Media.Pen ClampPen(Brush brush, double width, double length)
    {
        var start = ClampStart - (length - ClampLength) / 2;
        var pen = new System.Windows.Media.Pen(brush, width)
        {
            DashStyle = new DashStyle(new[] { length / width, (ClampPeriod - length) / width }, (ClampPeriod - start) / width),
            DashCap = PenLineCap.Flat
        };
        pen.Freeze();
        return pen;
    }

    private static Point AlongSide(List<Point> side, List<double> distances, int i, double s)
    {
        var k = (s - distances[i]) / Math.Max(0.0001, distances[i + 1] - distances[i]);
        return new Point(side[i].X + (side[i + 1].X - side[i].X) * k, side[i].Y + (side[i + 1].Y - side[i].Y) * k);
    }

    private static void TurboDrawBrutalClamps(DrawingContext dc, List<Point> bodyLeft, List<Point> bodyRight, List<double> distances)
    {
        var left = new List<Point>(bodyLeft.Count);
        var right = new List<Point>(bodyRight.Count);
        for (int j = 0; j < bodyLeft.Count; j++)
        {
            var mid = new Point((bodyLeft[j].X + bodyRight[j].X) / 2, (bodyLeft[j].Y + bodyRight[j].Y) / 2);
            left.Add(mid + (bodyLeft[j] - mid) * 1.2);
            right.Add(mid + (bodyRight[j] - mid) * 1.2);
        }
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var i = 0;
            for (var s = ClampStart; s + ClampLength < distances[^1]; s += ClampPeriod)
            {
                if (s < distances[0]) continue;
                var e = s + ClampLength;
                while (distances[i + 1] <= s) i++;
                var k = i;
                while (distances[k + 1] <= e) k++;
                ctx.BeginFigure(AlongSide(left, distances, i, s), true, true);
                for (int j = i + 1; j <= k; j++) ctx.LineTo(left[j], true, true);
                ctx.LineTo(AlongSide(left, distances, k, e), true, true);
                ctx.LineTo(AlongSide(right, distances, k, e), true, true);
                for (int j = k; j > i; j--) ctx.LineTo(right[j], true, true);
                ctx.LineTo(AlongSide(right, distances, i, s), true, true);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(SparkBrush, InkPen, geo);
    }

    private void TurboDrawBrutalRibbon(DrawingContext dc, Geometry geo)
    {
        var width = Cell * 0.78;
        if (Settings.DropShadow)
        {
            var shadow = new System.Windows.Media.Pen(ShadowBrush, width + 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            shadow.Freeze();
            dc.PushTransform(new TranslateTransform(4, 5));
            dc.DrawGeometry(null, shadow, geo);
            dc.Pop();
        }
        var ink = new System.Windows.Media.Pen(InkBrush, width + 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        ink.Freeze();
        var cable = new System.Windows.Media.Pen(CableBrush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        cable.Freeze();
        dc.DrawGeometry(null, ink, geo);
        dc.DrawGeometry(null, cable, geo);
        dc.DrawGeometry(null, ClampPen(InkBrush, width + 9, ClampLength + 4), geo);
        dc.DrawGeometry(null, ClampPen(SparkBrush, width + 5, ClampLength), geo);
    }

    private void TurboDrawBrutalDigestingRibbon(DrawingContext dc, Geometry body, List<Point> left, List<Point> right,
        List<double> distances, Point head, Point tail)
    {
        if (Settings.DropShadow)
        {
            dc.PushTransform(new TranslateTransform(4, 5));
            dc.DrawGeometry(ShadowBrush, null, body);
            dc.Pop();
        }
        dc.DrawGeometry(CableBrush, null, body);
        TurboDrawBrutalClamps(dc, left, right, distances);
        var outline = new System.Windows.Media.Pen(InkBrush, 2.5) { LineJoin = PenLineJoin.Round };
        outline.Freeze();
        dc.DrawGeometry(null, outline, body);
        TurboRibbonSheen(dc, left, right, head, tail, 0.2, Color.FromArgb(80, 255, 255, 255), 2.2);
    }

    private static readonly Brush RustBrush = Frozen(new RadialGradientBrush(Color.FromRgb(0x7A, 0x7E, 0x86), Color.FromRgb(0x22, 0x24, 0x29))
    {
        GradientOrigin = new Point(0.35, 0.3)
    });
    private static readonly Brush HeatBrush = Frozen(new RadialGradientBrush(new GradientStopCollection
    {
        new GradientStop(Color.FromRgb(0xFF, 0xE4, 0x5C), 0),
        new GradientStop(Color.FromRgb(0xFF, 0x6A, 0x1A), 0.55),
        new GradientStop(Color.FromArgb(0, 0xFF, 0x3B, 0), 1)
    }));

    private static void TurboDrawBrutalExhaust(DrawingContext dc, double rx, double ry, double opening, double t)
    {
        var fx = rx * 1.25;
        var fy = ry * 1.25;
        if (Settings.DropShadow) dc.DrawEllipse(ShadowBrush, null, new Point(2, 4), fx, fy);
        dc.DrawEllipse(SteelBrush, InkPen, new Point(), fx, fy);
        for (int i = 0; i < 6; i++)
        {
            var a = i * Math.PI / 3 + Math.PI / 6;
            dc.DrawEllipse(LightSteelBrush, ThinInkPen, new Point(Math.Cos(a) * rx * 0.98, Math.Sin(a) * ry * 0.98), Cell * 0.05, Cell * 0.05);
        }
        var hx = rx * (0.28 + 0.42 * opening);
        var hy = ry * (0.28 + 0.48 * opening);
        dc.DrawEllipse(InkBrush, null, new Point(), hx, hy);
        if (opening < 0.05) return;
        dc.PushOpacity(opening * (0.85 + 0.15 * Math.Sin(t * 23)));
        dc.DrawEllipse(HeatBrush, null, new Point(), hx * 0.95, hy * 0.95);
        dc.Pop();
    }

    private static void TurboDrawBrutalSlag(DrawingContext dc, Point pos, double size, double age, double fade, double seed)
    {
        dc.PushOpacity(fade * fade);
        if (Settings.DropShadow) dc.DrawEllipse(ShadowBrush, null, new Point(pos.X + 3, pos.Y + size * 0.38), size * 0.52, size * 0.14);
        for (int i = 0; i < 2; i++)
        {
            var k = (age * 0.7 + i * 0.5) % 1;
            var steam = new SolidColorBrush(Color.FromArgb((byte)(150 * (1 - k)), 0xC9, 0xCE, 0xD6));
            steam.Freeze();
            dc.DrawEllipse(steam, null, new Point(pos.X + size * (i - 0.5) * 0.4, pos.Y - size * (0.45 + k * 1.2)), size * (0.12 + k * 0.18), size * (0.12 + k * 0.18));
        }
        dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
        dc.PushTransform(new RotateTransform(Math.Sin(age * 5 + seed) * 8 * (1 - age / PoopLifetime)));
        var outline = new System.Windows.Media.Pen(InkBrush, 1.5) { LineJoin = PenLineJoin.Round };
        outline.Freeze();
        dc.DrawEllipse(RustBrush, outline, new Point(0, size * 0.24), size * 0.5, size * 0.23);
        dc.DrawEllipse(RustBrush, outline, new Point(-size * 0.025, 0), size * 0.37, size * 0.21);
        dc.PushTransform(new ScaleTransform(size, size));
        var curlPen = new System.Windows.Media.Pen(InkBrush, 1.5 / size) { LineJoin = PenLineJoin.Round };
        curlPen.Freeze();
        dc.DrawGeometry(RustBrush, curlPen, PoopCurl);
        dc.Pop();
        var heat = Math.Clamp(1.15 - age / PoopLifetime, 0, 1);
        if (heat > 0)
        {
            dc.PushOpacity(heat * 0.7);
            dc.DrawEllipse(HeatBrush, null, new Point(0, size * 0.3), size * 0.55, size * 0.16);
            dc.Pop();
            var crack = new System.Windows.Media.Pen(new SolidColorBrush(Color.FromArgb((byte)(255 * heat), 0xFF, 0x9A, 0x1A)), Math.Max(2, size * 0.07))
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            crack.Freeze();
            var cracks = new StreamGeometry();
            using (var ctx = cracks.Open())
            {
                ctx.BeginFigure(new Point(-size * 0.32, size * 0.22), false, false);
                ctx.LineTo(new Point(-size * 0.16, size * 0.3), true, true);
                ctx.LineTo(new Point(-size * 0.02, size * 0.2), true, true);
                ctx.LineTo(new Point(size * 0.14, size * 0.31), true, true);
                ctx.BeginFigure(new Point(-size * 0.2, -size * 0.02), false, false);
                ctx.LineTo(new Point(-size * 0.05, size * 0.05), true, true);
                ctx.LineTo(new Point(size * 0.12, -size * 0.04), true, true);
            }
            cracks.Freeze();
            dc.DrawGeometry(null, crack, cracks);
        }
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private static void TurboDrawBrutalFood(DrawingContext dc, int kind, double half)
    {
        switch (kind)
        {
            case 0:
                var nut = GetNutImage(HazardNut, Cell * 0.5);
                var size = nut.Width * half / (Cell * 0.5);
                dc.DrawImage(nut, new Rect(-size / 2, -size / 2, size, size));
                break;
            case 1:
                dc.PushTransform(new ScaleTransform(half * 2 / 17.5, half * 2 / 17.5));
                dc.PushTransform(new TranslateTransform(-9, -9));
                dc.DrawGeometry(SparkBrush, SparkPen, FoodGeometry(1));
                dc.Pop();
                dc.Pop();
                break;
            case 2:
                dc.DrawRoundedRectangle(LightSteelBrush, InkPen, new Rect(half * 0.72, -half * 0.28, half * 0.3, half * 0.56), 1.5, 1.5);
                dc.DrawRoundedRectangle(InkBrush, InkPen, new Rect(-half, -half * 0.6, half * 1.75, half * 1.2), 3, 3);
                for (int i = 0; i < 3; i++)
                    dc.DrawRectangle(AcidBrush, null, new Rect(-half * 0.78 + i * half * 0.5, -half * 0.38, half * 0.38, half * 0.76));
                break;
            default:
                dc.DrawRectangle(SteelBrush, InkPen, new Rect(-half * 0.2, -half * 0.24, half * 1.15, half * 0.48));
                for (int i = 0; i < 4; i++)
                    dc.DrawLine(ThinInkPen, new Point(-half * 0.05 + i * half * 0.26, -half * 0.24), new Point(half * 0.08 + i * half * 0.26, half * 0.24));
                dc.PushTransform(new TranslateTransform(-half * 0.45, 0));
                dc.PushTransform(new ScaleTransform(half * 0.55, half * 0.55));
                dc.DrawGeometry(SteelBrush, null, HexGeometry);
                dc.Pop();
                dc.Pop();
                var hex = HexGeometry.Clone();
                hex.Transform = new TransformGroup { Children = { new ScaleTransform(half * 0.55, half * 0.55), new TranslateTransform(-half * 0.45, 0) } };
                dc.DrawGeometry(null, InkPen, hex);
                break;
        }
    }
}
