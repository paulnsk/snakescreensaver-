using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SnakeScreensaver.Snake;

public sealed class SnakeTurboCanvas : Canvas
{
    internal SnakeGame? Host;

    private readonly Dictionary<int, RadialGradientBrush> _glow = new();
    private readonly Dictionary<int, RadialGradientBrush> _fill = new();
    private readonly Dictionary<int, SolidColorBrush> _strokeColors = new();
    private readonly Dictionary<int, System.Windows.Media.Pen> _pens = new();
    private readonly Dictionary<int, Geometry> _foodGeo = new();

    public void InvalidateNow() => InvalidateVisual();

    public RadialGradientBrush GetGlow(int hue) => Lookup(_glow, hue, MakeGlow);
    public RadialGradientBrush GetFill(int hue) => Lookup(_fill, hue, MakeFill);

    private static TV Lookup<TV>(Dictionary<int, TV> d, int hue, Func<int, TV> make)
        where TV : Freezable
    {
        if (d.TryGetValue(hue, out var v)) return v;
        v = make(hue);
        v.Freeze();
        d[hue] = v;
        return v;
    }

    private static RadialGradientBrush MakeGlow(int hue)
    {
        var c = SnakeGame.HslStatic(hue, 1.0, 0.55);
        var b = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(180, c.R, c.G, c.B), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
        return b;
    }

    private static RadialGradientBrush MakeFill(int hue)
    {
        var inner = SnakeGame.HslStatic(hue, 0.85, 0.82);
        var outer = SnakeGame.HslStatic(hue, 1.0, 0.5);
        var b = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.35, 0.35),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        b.GradientStops.Add(new GradientStop(inner, 0));
        b.GradientStops.Add(new GradientStop(outer, 1));
        return b;
    }

    public SolidColorBrush GetStrokeColor(int hue)
    {
        if (_strokeColors.TryGetValue(hue, out var v)) return v;
        var c = SnakeGame.HslStatic((hue + 320) % 360, 1.0, 0.6);
        var b = new SolidColorBrush(c);
        b.Freeze();
        _strokeColors[hue] = b;
        return b;
    }

    public System.Windows.Media.Pen GetStrokePen(int hue, double thickness)
    {
        var key = ((int)Math.Round(thickness * 10)) * 361 + hue;
        if (_pens.TryGetValue(key, out var v)) return v;
        var p = new System.Windows.Media.Pen(GetStrokeColor(hue), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        p.Freeze();
        _pens[key] = p;
        return p;
    }

    public Geometry GetFoodGeometry(int kind)
    {
        if (_foodGeo.TryGetValue(kind, out var v)) return v;
        var g = SnakeGame.FoodGeometryStatic(kind);
        g.Freeze();
        _foodGeo[kind] = g;
        return g;
    }

    public void ClearCaches()
    {
        _glow.Clear();
        _fill.Clear();
        _strokeColors.Clear();
        _pens.Clear();
        _foodGeo.Clear();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Host != null) Host.TurboRender(dc, this);
    }
}
