using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace SnakeScreensaver.Snake;

public partial class SnakeGame
{
    private SKBitmap? _skiaBmp;
    private SKCanvas? _skiaCanvas;
    private WriteableBitmap? _wbmp;
    private byte[]? _skiaCopy;
    private int _w, _h;
    private const double Dpi = 96.0;

    private readonly List<SkiaParticle> _skiaParticles = new();
    private double _skiaEatPopStart = -1;
    private const double SkiaEatPopDur = 0.22;

    private SKImageFilter? _segShadowFilter;
    private SKImageFilter? _segShadowFilterBig;

    private sealed class SkiaParticle
    {
        public double X, Y, Vx, Vy, Born, Life, Size;
        public SKColor Color;
    }

    private void SkiaInit()
    {
        _segShadowFilter?.Dispose();
        _segShadowFilterBig?.Dispose();
        _segShadowFilter = SKImageFilter.CreateBlur(8, 8);
        _segShadowFilterBig = SKImageFilter.CreateBlur(10, 10);
        SkiaResize();
    }

    private void SkiaResize()
    {
        var w = (int)Math.Max(1, ActualWidth);
        var h = (int)Math.Max(1, ActualHeight);
        if (w == _w && h == _h && _skiaBmp != null) return;
        _skiaBmp?.Dispose();
        _skiaCanvas?.Dispose();
        _wbmp = null;
        _w = w; _h = h;
        var info = new SKImageInfo(_w, _h, SKColorType.Bgra8888, SKAlphaType.Premul);
        _skiaBmp = new SKBitmap(info);
        _skiaCanvas = new SKCanvas(_skiaBmp);
        _wbmp = new WriteableBitmap(_w, _h, Dpi, Dpi, PixelFormats.Bgra32, null);
        _skiaCopy = new byte[_w * _h * 4];
        if (!ReferenceEquals(SkiaImage.Source, _wbmp)) SkiaImage.Source = _wbmp;
    }

    private void SkiaReset()
    {
        _skiaParticles.Clear();
        _skiaEatPopStart = -1;
    }

    private Seg SkiaCreateSeg()
    {
        return new Seg { Half = Cell * 0.46 };
    }

    private void SkiaAddFood(Food f)
    {
        f.SpawnTime = Now;
    }

    private void SkiaEatFood(Food f)
    {
        _foods.Remove(f);
        var pos = CellCenter(f.Cell);
        SkiaBurst(pos, f.Hue, 28);
        _hueOffset = (_hueOffset + 33.0) % 360.0;
        _skiaEatPopStart = Now;
    }

    private void SkiaBurst(Point center, double hue, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var ph = (hue + _rng.Next(-30, 30) + 360) % 360.0;
            var l = _rng.NextDouble() * 0.2 + 0.55;
            var c = HslToSkia(ph, 1.0, l);
            var ang = _rng.NextDouble() * Math.PI * 2;
            var spd = _rng.Next(40, 170);
            _skiaParticles.Add(new SkiaParticle
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
        if (_skiaParticles.Count > 1200) _skiaParticles.RemoveRange(0, _skiaParticles.Count - 1200);
    }

    private void SkiaRedraw(double t)
    {
        if (_skiaCanvas == null || _skiaBmp == null || _wbmp == null || _skiaCopy == null) return;
        var canvas = _skiaCanvas;
        canvas.Clear(new SKColor(0, 0, 0, 0));

        SkiaDrawFoods(canvas, t);
        if (_mode == Settings.RenderMode.Balls) SkiaDrawBalls(canvas, t);
        else SkiaDrawRibbon(canvas, t);
        SkiaDrawParticles(canvas, t);

        canvas.Flush();
        _skiaBmp.GetPixelSpan().CopyTo(_skiaCopy);
        _wbmp.Lock();
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(_skiaCopy, 0, _wbmp.BackBuffer, _skiaCopy.Length);
            _wbmp.AddDirtyRect(new Int32Rect(0, 0, _w, _h));
        }
        finally
        {
            _wbmp.Unlock();
        }
    }

    private void SkiaDrawFoods(SKCanvas canvas, double t)
    {
        if (_foods.Count == 0) return;
        var w = Cell * 0.78;
        var h = Cell * 0.78;
        using var fillPaint = new SKPaint { IsAntialias = true };
        using var strokePaint = new SKPaint { IsAntialias = true, IsStroke = true, StrokeWidth = 1.8f };
        if (_segShadowFilterBig == null) return;

        foreach (var f in _foods)
        {
            var pos = CellCenter(f.Cell);
            var age = t - f.SpawnTime;
            if (age < 0) age = 0;
            var pulseFreq = f.PulseFreq;
            var pulse = 1.0 + 0.1 * Math.Sin(age * pulseFreq);
            var rot = age * f.RotSpeed;

            var halfW = (float)(w * 0.5 * pulse);
            var halfH = (float)(h * 0.5 * pulse);
            using var path = FoodSkiaPath(f.Kind, w * pulse, h * pulse);

            strokePaint.Color = HslToSkia((f.Hue + 320) % 360.0, 1.0, 0.7);

            canvas.Save();
            canvas.Translate((float)pos.X, (float)pos.Y);
            canvas.RotateDegrees((float)(rot * 360));

            using (var sLayerPaint = new SKPaint())
            {
                sLayerPaint.Color = HslToSkia(f.Hue, 1.0, 0.55).WithAlpha(120);
                if (Settings.DropShadow) sLayerPaint.ImageFilter = _segShadowFilterBig;
                canvas.DrawCircle(2, 4, halfW * 1.2f, sLayerPaint);
            }

            fillPaint.Shader?.Dispose();
            fillPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(0, 0),
                halfW,
                new[] { HslToSkia(f.Hue, 0.85, 0.85), HslToSkia(f.Hue, 1.0, 0.45) },
                SKShaderTileMode.Clamp);
            canvas.DrawPath(path, fillPaint);
            canvas.DrawPath(path, strokePaint);

            canvas.Restore();
        }
        fillPaint.Shader?.Dispose();
        fillPaint.Shader = null;
    }

    private static SKPath FoodSkiaPath(int kind, double w, double h)
    {
        var path = new SKPath();
        var hw = (float)(w * 0.5);
        var hh = (float)(h * 0.5);
        switch (kind)
        {
            case 0:
                path.AddCircle(0f, 0f, hw);
                break;
            case 1:
                var star = new SKPoint[10];
                for (int i = 0; i < 10; i++)
                {
                    var ang = -Math.PI / 2 + i * Math.PI / 5;
                    var r = i % 2 == 0 ? hw : hw * 0.4f;
                    star[i] = new SKPoint((float)(Math.Cos(ang) * r), (float)(Math.Sin(ang) * r));
                }
                path.AddPoly(star, true);
                break;
            case 2:
                path.MoveTo(0f, hh);
                path.CubicTo(-hw * 0.8f, hh * 0.3f, -hw * 0.7f, -hh * 0.6f, -hw * 0.2f, -hh * 0.6f);
                path.CubicTo(-hw * 0.1f, -hh * 0.6f, -hw * 0.05f, -hh * 0.4f, 0f, -hh * 0.2f);
                path.CubicTo(hw * 0.05f, -hh * 0.4f, hw * 0.1f, -hh * 0.6f, hw * 0.2f, -hh * 0.6f);
                path.CubicTo(hw * 0.7f, -hh * 0.6f, hw * 0.8f, hh * 0.3f, 0f, hh);
                path.Close();
                break;
            case 3:
                path.AddPoly(new[]
                {
                    new SKPoint(0f, -hh),
                    new SKPoint(hw, 0f),
                    new SKPoint(0f, hh),
                    new SKPoint(-hw, 0f),
                }, true);
                break;
            case 4:
                path.AddPoly(new[]
                {
                    new SKPoint(0f, -hh),
                    new SKPoint(hw, -hh * 0.4f),
                    new SKPoint(hw, hh * 0.4f),
                    new SKPoint(0f, hh),
                    new SKPoint(-hw, hh * 0.4f),
                    new SKPoint(-hw, -hh * 0.4f),
                }, true);
                break;
        }
        return path;
    }

    private void SkiaDrawBalls(SKCanvas canvas, double t)
    {
        if (_segs.Count == 0) return;
        var radius = Cell * 0.46;

        var pop = 1.0;
        if (_skiaEatPopStart > 0)
        {
            var pp = (t - _skiaEatPopStart) / SkiaEatPopDur;
            if (pp < 1) pop = 1 + 0.45 * Math.Sin(pp * Math.PI);
            else _skiaEatPopStart = -1;
        }

        using var fillPaint = new SKPaint { IsAntialias = true };
        using var strokePaint = new SKPaint { IsAntialias = true, IsStroke = true, StrokeWidth = 2 };
        using var shadowPaint = new SKPaint { IsAntialias = true };

        for (int i = 0; i < _segs.Count; i++)
        {
            var seg = _segs[i];
            var progress = seg.MoveDuration > 0 ? Math.Min(1, (t - seg.MoveStart) / seg.MoveDuration) : 1;
            var eased = 1 - Math.Pow(1 - progress, 3);
            var curX = seg.Stored.X + (seg.To.X - seg.Stored.X) * eased;
            var curY = seg.Stored.Y + (seg.To.Y - seg.Stored.Y) * eased;
            var hue = (_hueOffset + t * 55.0 + i * 11.0) % 360.0;
            var r = radius * (i == 0 ? pop : 1);

            shadowPaint.Color = HslToSkia(hue, 1.0, 0.55).WithAlpha(160);
            shadowPaint.ImageFilter = Settings.DropShadow ? _segShadowFilter : null;
            canvas.DrawCircle((float)(curX + 4), (float)(curY + 6), (float)(r * 1.1), shadowPaint);
            shadowPaint.ImageFilter = null;

            var center = new SKPoint((float)(curX - r * 0.15), (float)(curY - r * 0.15));
            fillPaint.Shader?.Dispose();
            fillPaint.Shader = SKShader.CreateRadialGradient(
                center,
                (float)r,
                new[] { HslToSkia(hue, 0.85, 0.82), HslToSkia(hue, 1.0, 0.5) },
                SKShaderTileMode.Clamp);
            canvas.DrawCircle((float)curX, (float)curY, (float)r, fillPaint);

            strokePaint.Color = HslToSkia((hue + 320) % 360.0, 1.0, 0.6);
            canvas.DrawCircle((float)curX, (float)curY, (float)r, strokePaint);
        }
        fillPaint.Shader?.Dispose();
        fillPaint.Shader = null;
    }

    private void SkiaDrawRibbon(SKCanvas canvas, double t)
    {
        if (_ribbonSamples.Count < 2 || !_ribbonReady) return;
        var head = _ribbonSamples[0];
        var tail = _ribbonSamples[^1];
        if (Len(head, tail) < 0.001) tail = new Point(head.X + 1, head.Y);

        using var path = new SKPath();
        path.MoveTo((float)head.X, (float)head.Y);
        for (int i = 1; i < _ribbonSamples.Count; i++)
            path.LineTo((float)_ribbonSamples[i].X, (float)_ribbonSamples[i].Y);

        var colors = new SKColor[6];
        var positions = new float[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };
        for (int i = 0; i < 6; i++)
        {
            var h = (_hueOffset + t * 45.0 + i * 55.0) % 360.0;
            var alpha = i == 5 ? (byte)0 : (byte)255;
            colors[i] = HslToSkia(h, 1.0, 0.62).WithAlpha(alpha);
        }

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            IsStroke = true,
            StrokeWidth = (float)(Cell * 0.78),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        strokePaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint((float)head.X, (float)head.Y),
            new SKPoint((float)tail.X, (float)tail.Y),
            colors,
            positions,
            SKShaderTileMode.Clamp);

        using var shadowPaint = new SKPaint
        {
            IsAntialias = true,
            IsStroke = true,
            StrokeWidth = (float)(Cell * 0.78),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        shadowPaint.Color = HslToSkia((_hueOffset + t * 60.0) % 360.0, 1.0, 0.55).WithAlpha(150);
        shadowPaint.ImageFilter = Settings.DropShadow ? _segShadowFilterBig : null;

        canvas.DrawPath(path, shadowPaint);
        shadowPaint.ImageFilter = null;

        canvas.DrawPath(path, strokePaint);
        strokePaint.Shader?.Dispose();
        strokePaint.Shader = null;
    }

    private void SkiaDrawParticles(SKCanvas canvas, double t)
    {
        if (_skiaParticles.Count == 0) return;
        using var paint = new SKPaint { IsAntialias = true };
        if (_segShadowFilter == null) return;
        for (int i = _skiaParticles.Count - 1; i >= 0; i--)
        {
            var p = _skiaParticles[i];
            var age = t - p.Born;
            if (age >= p.Life) { _skiaParticles.RemoveAt(i); continue; }
            var eased = 1 - Math.Pow(1 - age / p.Life, 2);
            var x = p.X + p.Vx * eased * 0.5;
            var y = p.Y + p.Vy * eased * 0.5 - 20 * age * age;
            var alpha = (byte)(255 * (1 - age / p.Life));
            var size = p.Size * (1 - 0.4 * age / p.Life);
            paint.Color = p.Color.WithAlpha(alpha);
            paint.ImageFilter = _segShadowFilter;
            canvas.DrawCircle((float)x, (float)y, (float)(size * 0.5), paint);
        }
        paint.ImageFilter = null;
    }

    private static SKColor HslToSkia(double h, double s, double l)
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
        return new SKColor(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }
}
