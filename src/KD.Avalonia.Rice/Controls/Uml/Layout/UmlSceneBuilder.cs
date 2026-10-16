using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Builds the scene of any diagram: picks the layout of its kind and adds the title.</summary>
internal static class UmlSceneBuilder
{
    public static UmlScene Build(UmlDiagram? diagram, UmlStyle style)
    {
        if (diagram is null)
            return new UmlScene();
        return WithTitle(diagram.Title, style, (scene, origin) =>
        {
            switch (diagram)
            {
                case UmlGraphDiagram graph:
                    new GraphSceneBuilder(graph, style).Build(scene, origin);
                    break;
                case UmlActivityDiagram activity:
                    new ActivitySceneBuilder(activity, style).Build(scene, origin);
                    break;
                case UmlSequenceDiagram sequence:
                    new SequenceSceneBuilder(sequence, style).Build(scene, origin);
                    break;
                case UmlTimingDiagram timing:
                    new TimingSceneBuilder(timing, style).Build(scene, origin);
                    break;
            }
        });
    }

    /// <summary>Builds a scene below its title (when there is one), the title centered over the drawing.</summary>
    public static UmlScene WithTitle(string? title, UmlStyle style, Action<UmlScene, Point> build)
    {
        var scene = new UmlScene();
        var titleFontSize = style.FontSize + 5;
        var titleSize = title is { Length: > 0 } ? style.Measurer.Measure(title, titleFontSize, bold: true) : default;
        build(scene, new Point(0, titleSize.Height > 0 ? titleSize.Height + 20 : 0));

        var bounds = BoundsOf(scene);
        if (title is { Length: > 0 } text)
        {
            var center = bounds.Width > 0 ? bounds.Center.X : titleSize.Width / 2;
            var prim = scene.Add(new TextPrim
            {
                Text = text,
                Origin = new Point(center - titleSize.Width / 2, 0),
                Size = titleSize,
                FontSize = titleFontSize,
                Bold = true,
                Center = true,
            });
            bounds = bounds.Width > 0 ? bounds.Union(new Rect(prim.Origin, prim.Size)) : new Rect(prim.Origin, prim.Size);
        }
        scene.Bounds = bounds;
        return scene;
    }

    public static Rect BoundsOf(UmlScene scene)
    {
        Rect? bounds = null;
        void Add(Rect r)
        {
            if (r.Width <= 0 && r.Height <= 0)
                return;
            bounds = bounds is { } b ? b.Union(r) : r;
        }

        foreach (var prim in scene.Prims)
        {
            switch (prim)
            {
                case ShapePrim s:
                    Add(s.Rect);
                    break;
                case TextPrim t:
                    Add(new Rect(t.Origin, t.Size));
                    break;
                case LinePrim l:
                    Add(UmlGeometry.BoundsOf(UmlGeometry.Flatten(l.Points, l.Curve)));
                    break;
                case HeadPrim h:
                    Add(new Rect(h.Tip.X - 10, h.Tip.Y - 10, 20, 20));
                    break;
                case GlyphPrim g:
                    Add(g.Rect);
                    break;
                case WedgePrim w:
                    Add(new Rect(w.Center.X - w.Radius, w.Center.Y - w.Radius, 2 * w.Radius, 2 * w.Radius));
                    break;
            }
        }
        return bounds ?? default;
    }
}
