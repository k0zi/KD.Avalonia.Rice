using Avalonia;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Measures the elements of graph diagrams and turns them into drawing primitives.</summary>
internal sealed class UmlNodePainter(UmlStyle style, UmlDiagramKind diagramKind, bool leftToRight)
{
    private const double BadgeSize = 18;
    private const double GlyphColumn = 14;
    private const double PortSize = 12;

    private double Pad => style.Padding + 2;

    public static bool IsClassifier(UmlElementKind kind) => kind is UmlElementKind.Class or UmlElementKind.AbstractClass
        or UmlElementKind.Interface or UmlElementKind.Enum or UmlElementKind.Annotation or UmlElementKind.Entity
        or UmlElementKind.Metaclass or UmlElementKind.Stereotype or UmlElementKind.Object;

    public UmlAnchor Anchor(UmlElement e) => e.Kind switch
    {
        UmlElementKind.UseCase or UmlElementKind.InitialState or UmlElementKind.FinalState or UmlElementKind.History
            or UmlElementKind.DeepHistory or UmlElementKind.EntryPoint or UmlElementKind.ExitPoint
            or UmlElementKind.InterfaceCircle or UmlElementKind.Cloud => UmlAnchor.Ellipse,
        UmlElementKind.Choice => UmlAnchor.Diamond,
        _ => UmlAnchor.Rectangle,
    };

    /// <summary>The part of the element lines attach to (the circle of a lollipop, the figure of an actor).</summary>
    public Rect AnchorRect(UmlElement e, Rect bounds) => e.Kind switch
    {
        UmlElementKind.InterfaceCircle => new Rect(bounds.Center.X - 9, bounds.Y, 18, 18),
        _ => bounds,
    };

    // ---- leaves ----

    public Size Measure(UmlElement e)
    {
        if (e.Size is { } fixedSize)
            return fixedSize;

        var name = e.Name;
        var nameSize = style.Measure(name, bold: true);
        var stereo = Stereotypes(e);
        var stereoSize = style.Measure(stereo, small: true);
        var textWidth = Math.Max(nameSize.Width, stereoSize.Width);
        var textHeight = nameSize.Height + stereoSize.Height;

        if (IsClassifier(e.Kind))
            return MeasureClassifier(e);

        switch (e.Kind)
        {
            case UmlElementKind.Note:
            {
                var size = style.Measure(e.Text ?? "");
                return new Size(Math.Max(60, size.Width + Pad * 2 + 8), Math.Max(32, size.Height + Pad * 1.6));
            }
            case UmlElementKind.Actor or UmlElementKind.Person:
                return new Size(Math.Max(36, nameSize.Width), 44 + 6 + textHeight);
            case UmlElementKind.Boundary or UmlElementKind.Control or UmlElementKind.EntityIcon:
                return new Size(Math.Max(40, nameSize.Width), 30 + 6 + textHeight);
            case UmlElementKind.UseCase:
                return new Size(Math.Max(110, textWidth * 1.3 + 32), Math.Max(44, textHeight * 1.5 + 18));
            case UmlElementKind.InterfaceCircle:
            {
                var label = style.Measure(name, small: true);
                return new Size(Math.Max(24, label.Width), 18 + 4 + label.Height);
            }
            case UmlElementKind.InitialState:
                return new Size(18, 18);
            case UmlElementKind.FinalState:
                return new Size(22, 22);
            case UmlElementKind.Choice:
                return new Size(24, 24);
            case UmlElementKind.Fork or UmlElementKind.Join:
                return leftToRight ? new Size(6, 70) : new Size(70, 6);
            case UmlElementKind.History or UmlElementKind.DeepHistory:
                return new Size(26, 26);
            case UmlElementKind.EntryPoint or UmlElementKind.ExitPoint:
                return new Size(16, 16);
            case UmlElementKind.Port or UmlElementKind.PortIn or UmlElementKind.PortOut:
                return new Size(PortSize, PortSize);
            case UmlElementKind.State:
            {
                var members = e.Members.Where(m => m.Kind != UmlMemberKind.Separator).Select(m => style.Measure(m.Text, small: true)).ToList();
                var width = Math.Max(nameSize.Width, members.Count == 0 ? 0 : members.Max(s => s.Width));
                var height = textHeight + Pad * 1.6 + (members.Count == 0 ? 0 : members.Sum(s => s.Height + 2) + Pad);
                return new Size(Math.Max(96, width + Pad * 2.4), Math.Max(40, height));
            }
            case UmlElementKind.Label:
                return new Size(textWidth + 8, textHeight + 6);
        }

        var (extraW, extraH) = e.Kind switch
        {
            UmlElementKind.Node => (14.0, 14.0),
            UmlElementKind.Database => (0.0, 22.0),
            UmlElementKind.Cloud => (textWidth * 0.4 + 24, textHeight * 0.6 + 14),
            UmlElementKind.Folder or UmlElementKind.Package => (0.0, 14.0),
            UmlElementKind.Frame => (0.0, 18.0),
            UmlElementKind.Component or UmlElementKind.Artifact or UmlElementKind.File => (22.0, 0.0),
            UmlElementKind.Hexagon => (24.0, 0.0),
            UmlElementKind.Collections => (6.0, 6.0),
            _ => (0.0, 0.0),
        };
        var ports = e.Children.Where(c => c.IsPort).ToList();
        var sidePorts = Math.Max(ports.Count(p => p.Kind == UmlElementKind.PortIn), ports.Count(p => p.Kind == UmlElementKind.PortOut));
        var topPorts = ports.Count(p => p.Kind == UmlElementKind.Port);
        return new Size(
            Math.Max(Math.Max(110, topPorts * 30 + 20), textWidth + Pad * 2.4 + extraW),
            Math.Max(Math.Max(50, sidePorts * 26 + 16), textHeight + Pad * 2 + extraH));
    }

    public void Paint(UmlScene scene, UmlElement e, Rect r, int item)
    {
        if (IsClassifier(e.Kind))
        {
            PaintClassifier(scene, e, r, item);
            return;
        }

        var stereo = Stereotypes(e);
        switch (e.Kind)
        {
            case UmlElementKind.Note:
                scene.Add(new ShapePrim { Shape = UmlShape.Note, Rect = r, Fill = UmlBrush.Note, Stroke = UmlBrush.NoteBorder, Shadow = true }, item);
                scene.Add(style.Text(e.Text ?? "", new Point(r.X + Pad, r.Y + Pad * 0.8)), item);
                return;

            case UmlElementKind.Actor or UmlElementKind.Person:
            {
                var figure = new Rect(r.Center.X - 14, r.Y, 28, 44);
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.StickFigure, Rect = figure, Brush = UmlBrush.Line, Fill = UmlBrush.Node }, item);
                PaintCaption(scene, e, stereo, r.Center.X, figure.Bottom + 6, item);
                return;
            }

            case UmlElementKind.Boundary or UmlElementKind.Control or UmlElementKind.EntityIcon:
            {
                var icon = new Rect(r.Center.X - 18, r.Y, 36, 30);
                var glyph = e.Kind switch
                {
                    UmlElementKind.Boundary => UmlGlyph.Boundary,
                    UmlElementKind.Control => UmlGlyph.Control,
                    _ => UmlGlyph.Entity,
                };
                scene.Add(new GlyphPrim { Glyph = glyph, Rect = icon, Brush = UmlBrush.Line, Fill = UmlBrush.Node }, item);
                PaintCaption(scene, e, stereo, r.Center.X, icon.Bottom + 6, item);
                return;
            }

            case UmlElementKind.UseCase:
                scene.Add(new ShapePrim { Shape = UmlShape.Ellipse, Rect = r, Shadow = true }, item);
                PaintCenteredName(scene, e, stereo, r, item);
                return;

            case UmlElementKind.InterfaceCircle:
            {
                var circle = AnchorRect(e, r);
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.Circle, Rect = circle, Brush = UmlBrush.Line, Fill = UmlBrush.Node }, item);
                scene.Add(style.CenteredText(e.Name, r.Center.X, circle.Bottom + 4, small: true), item);
                return;
            }

            case UmlElementKind.InitialState:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.Dot, Rect = r, Fill = UmlBrush.Solid }, item);
                return;
            case UmlElementKind.FinalState:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.Bullseye, Rect = r, Brush = UmlBrush.Solid, Fill = UmlBrush.Background }, item);
                return;
            case UmlElementKind.Choice:
                scene.Add(new ShapePrim { Shape = UmlShape.Diamond, Rect = r }, item);
                return;
            case UmlElementKind.Fork or UmlElementKind.Join:
                scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 3, Fill = UmlBrush.Solid, Stroke = UmlBrush.None }, item);
                return;
            case UmlElementKind.History or UmlElementKind.DeepHistory:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.Badge, Rect = r, Letter = e.Kind == UmlElementKind.History ? "H" : "H*", Brush = UmlBrush.Text, Fill = UmlBrush.Node }, item);
                return;
            case UmlElementKind.EntryPoint:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.Circle, Rect = r, Brush = UmlBrush.Line, Fill = UmlBrush.Node }, item);
                return;
            case UmlElementKind.ExitPoint:
                scene.Add(new GlyphPrim { Glyph = UmlGlyph.CircleCross, Rect = r, Brush = UmlBrush.Line, Fill = UmlBrush.Node }, item);
                return;
            case UmlElementKind.Port or UmlElementKind.PortIn or UmlElementKind.PortOut:
                scene.Add(new ShapePrim { Shape = UmlShape.Rectangle, Rect = r, Fill = UmlBrush.Node, Stroke = UmlBrush.Line }, item);
                return;

            case UmlElementKind.State:
                PaintState(scene, e, r, item);
                return;

            case UmlElementKind.Label:
                PaintCenteredName(scene, e, stereo, r, item);
                return;
        }

        PaintBox(scene, e, r, item, container: false);
        var content = ContentRect(e, r);
        PaintCenteredName(scene, e, stereo, content, item);
    }

    // ---- containers ----

    /// <summary>Space around the children of a container: title on top, padding on the sides.</summary>
    public Thickness ContainerPadding(UmlElement e)
    {
        var header = HeaderHeight(e);
        var pad = 16.0;
        return e.Kind switch
        {
            UmlElementKind.Node => new Thickness(pad, header + 10 + pad * 0.5, pad + 10, pad),
            UmlElementKind.Database => new Thickness(pad, header + 24, pad, pad + 12),
            UmlElementKind.Cloud => new Thickness(pad * 2, header + pad, pad * 2, pad * 1.5),
            _ => new Thickness(pad, header + pad * 0.6, pad, pad),
        };
    }

    public double HeaderHeight(UmlElement e)
    {
        var name = style.Measure(e.Name, bold: true);
        var stereo = style.Measure(Stereotypes(e), small: true);
        return name.Height + stereo.Height + (e.Kind is UmlElementKind.Package or UmlElementKind.Folder ? 8 : 14);
    }

    public Size MinContainerSize(UmlElement e)
    {
        var name = style.Measure(e.Name, bold: true);
        var stereo = style.Measure(Stereotypes(e), small: true);
        return new Size(Math.Max(name.Width, stereo.Width) + 48, HeaderHeight(e) + 24);
    }

    public void PaintContainer(UmlScene scene, UmlElement e, Rect r, int item)
    {
        var stereo = Stereotypes(e);
        var header = HeaderHeight(e);
        if (e.Kind == UmlElementKind.State)
        {
            scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 14, Fill = UmlBrush.Container, Stroke = UmlBrush.Border, Shadow = false }, item);
            PaintCaption(scene, e, stereo, r.Center.X, r.Y + 7, item);
            scene.Add(new LinePrim { Points = [new Point(r.X, r.Y + header), new Point(r.Right, r.Y + header)], Brush = UmlBrush.Border, Thickness = 1 }, item);
            return;
        }

        PaintBox(scene, e, r, item, container: true);
        var left = e.Kind switch
        {
            UmlElementKind.Package or UmlElementKind.Folder => r.X + 10,
            UmlElementKind.Frame => r.X + 8,
            _ => r.X + 12,
        };
        var top = e.Kind switch
        {
            UmlElementKind.Package or UmlElementKind.Folder => r.Y + 4,
            UmlElementKind.Node => r.Y + 10 + 6,
            UmlElementKind.Database => r.Y + 20,
            UmlElementKind.Cloud => r.Y + 16,
            UmlElementKind.Frame => r.Y + 3,
            _ => r.Y + 8,
        };
        if (e.Kind is UmlElementKind.Rectangle or UmlElementKind.Cloud)
        {
            PaintCaption(scene, e, stereo, r.Center.X, top, item);
            return;
        }
        var y = top;
        if (stereo.Length > 0)
        {
            var s = scene.Add(style.Text(stereo, new Point(left, y), small: true, brush: UmlBrush.MutedText), item);
            y += s.Size.Height;
        }
        scene.Add(style.Text(e.Name, new Point(left, y), bold: true), item);
    }

    /// <summary>Box shapes shared by leaves and containers (component, node, package, database, …).</summary>
    private void PaintBox(UmlScene scene, UmlElement e, Rect r, int item, bool container)
    {
        var fill = container ? UmlBrush.Container : UmlBrush.Node;
        var shadow = !container;
        switch (e.Kind)
        {
            case UmlElementKind.Node:
                scene.Add(new ShapePrim { Shape = UmlShape.Node3D, Rect = r, Extra = 10, Fill = fill, Shadow = shadow }, item);
                break;
            case UmlElementKind.Database:
                scene.Add(new ShapePrim { Shape = UmlShape.Cylinder, Rect = r, Extra = 10, Fill = fill, Shadow = shadow }, item);
                break;
            case UmlElementKind.Cloud:
                scene.Add(new ShapePrim { Shape = UmlShape.Cloud, Rect = r, Fill = fill, Shadow = shadow }, item);
                break;
            case UmlElementKind.Folder or UmlElementKind.Package:
            {
                var tabWidth = container
                    ? Math.Min(r.Width - 10, Math.Max(style.Measure(e.Name, bold: true).Width, style.Measure(Stereotypes(e), small: true).Width) + 20)
                    : Math.Min(r.Width * 0.4, 56);
                var tabHeight = container ? HeaderHeight(e) : 12;
                scene.Add(new ShapePrim { Shape = UmlShape.Folder, Rect = r, Extra = tabWidth, Extra2 = tabHeight, Radius = 4, Fill = fill, Shadow = shadow }, item);
                break;
            }
            case UmlElementKind.Frame:
            {
                var tag = style.Measure(e.Name, bold: true);
                scene.Add(new ShapePrim { Shape = UmlShape.Frame, Rect = r, Extra = tag.Width + 22, Extra2 = Math.Max(18, tag.Height + 6), Fill = fill, Shadow = shadow }, item);
                break;
            }
            case UmlElementKind.Hexagon:
                scene.Add(new ShapePrim { Shape = UmlShape.Hexagon, Rect = r, Fill = fill, Shadow = shadow }, item);
                break;
            case UmlElementKind.Collections:
                scene.Add(new ShapePrim { Shape = UmlShape.Collections, Rect = r, Radius = 4, Fill = fill, Shadow = shadow }, item);
                break;
            case UmlElementKind.Storage:
                scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = Math.Min(r.Height / 2, 18), Fill = fill, Shadow = shadow }, item);
                break;
            case UmlElementKind.Rectangle when container:
                scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 10, Fill = fill, Thickness = 1.5 }, item);
                break;
            default:
                scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 6, Fill = fill, Shadow = shadow }, item);
                break;
        }

        var icon = e.Kind switch
        {
            UmlElementKind.Component => UmlGlyph.ComponentIcon,
            UmlElementKind.Artifact or UmlElementKind.File => UmlGlyph.ArtifactIcon,
            _ => (UmlGlyph?)null,
        };
        if (icon is { } glyph)
            scene.Add(new GlyphPrim { Glyph = glyph, Rect = new Rect(r.Right - 22, r.Y + 8, 14, 14), Brush = UmlBrush.MutedText, Fill = UmlBrush.Node }, item);
    }

    /// <summary>The area of a box shape the text goes in (without the 3D depth, the tab, the cylinder caps).</summary>
    private static Rect ContentRect(UmlElement e, Rect r) => e.Kind switch
    {
        UmlElementKind.Node => new Rect(r.X, r.Y + 10, r.Width - 10, r.Height - 10),
        UmlElementKind.Database => new Rect(r.X, r.Y + 12, r.Width, r.Height - 12),
        UmlElementKind.Folder or UmlElementKind.Package => new Rect(r.X, r.Y + 12, r.Width, r.Height - 12),
        UmlElementKind.Frame => new Rect(r.X, r.Y + 14, r.Width, r.Height - 14),
        UmlElementKind.Component or UmlElementKind.Artifact or UmlElementKind.File => new Rect(r.X, r.Y, r.Width - 12, r.Height),
        _ => r,
    };

    // ---- classifiers ----

    private sealed record ClassifierLayout(double HeaderHeight, List<List<UmlMember>> Compartments, bool HasVisibility, double LineHeight);

    private ClassifierLayout LayoutClassifier(UmlElement e)
    {
        var name = style.Measure(e.Name, bold: true);
        var stereo = style.Measure(Stereotypes(e), small: true);
        var header = Math.Max(BadgeSize, name.Height + stereo.Height) + Pad * 1.4;

        var compartments = new List<List<UmlMember>>();
        if (e.Members.Any(m => m.Kind == UmlMemberKind.Separator))
        {
            var current = new List<UmlMember>();
            foreach (var m in e.Members)
            {
                if (m.Kind == UmlMemberKind.Separator)
                {
                    if (current.Count > 0)
                        compartments.Add(current);
                    current = [];
                    if (m.Text.Length > 0)
                        current.Add(m);
                }
                else
                    current.Add(m);
            }
            if (current.Count > 0)
                compartments.Add(current);
        }
        else
        {
            var fields = e.Members.Where(m => !m.IsMethod || e.Kind == UmlElementKind.Enum).ToList();
            var methods = e.Members.Where(m => m.IsMethod && e.Kind != UmlElementKind.Enum).ToList();
            if (fields.Count > 0)
                compartments.Add(fields);
            if (methods.Count > 0)
                compartments.Add(methods);
        }

        var lineHeight = style.Measure("Xg").Height + 3;
        return new ClassifierLayout(header, compartments, e.Members.Any(m => m.Visibility != UmlVisibility.None), lineHeight);
    }

    private Size MeasureClassifier(UmlElement e)
    {
        var layout = LayoutClassifier(e);
        var name = style.Measure(e.Name, bold: true, italic: IsItalic(e));
        var stereo = style.Measure(Stereotypes(e), small: true);
        var badge = HasBadge(e) ? BadgeSize + 8 : 0;
        var width = badge + Math.Max(name.Width, stereo.Width);
        var height = layout.HeaderHeight;
        foreach (var compartment in layout.Compartments)
        {
            height += Pad * 0.8;
            foreach (var m in compartment)
            {
                var size = MemberSize(m);
                width = Math.Max(width, size.Width + (layout.HasVisibility && m.Kind != UmlMemberKind.Separator ? GlyphColumn : 0));
                height += layout.LineHeight;
            }
        }
        var minWidth = e.Kind == UmlElementKind.Object ? 90 : 120;
        return new Size(Math.Max(minWidth, width + Pad * 2), height);
    }

    private Size MemberSize(UmlMember m) =>
        m.Kind == UmlMemberKind.Separator ? style.Measure(m.Text, bold: true, small: true) : style.Measure(m.Text, italic: m.IsAbstract);

    private void PaintClassifier(UmlScene scene, UmlElement e, Rect r, int item)
    {
        var layout = LayoutClassifier(e);
        scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 6, Shadow = true }, item);
        scene.Add(new ShapePrim
        {
            Shape = UmlShape.TopRounded,
            Rect = new Rect(r.X, r.Y, r.Width, layout.HeaderHeight),
            Radius = 6,
            Fill = UmlBrush.Header,
            Stroke = UmlBrush.None,
        }, item);
        // The border again on top of the header band, so it stays crisp.
        scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = 6, Fill = UmlBrush.None }, item);

        var stereo = Stereotypes(e);
        var name = style.Measure(e.Name, bold: true, italic: IsItalic(e));
        var stereoSize = style.Measure(stereo, small: true);
        var badge = HasBadge(e);
        var block = (badge ? BadgeSize + 8 : 0) + Math.Max(name.Width, stereoSize.Width);
        var x = r.Center.X - block / 2;
        var textHeight = name.Height + stereoSize.Height;
        if (badge)
        {
            var (letter, brush) = Badge(e.Kind);
            scene.Add(new GlyphPrim
            {
                Glyph = UmlGlyph.Badge,
                Rect = new Rect(x, r.Y + (layout.HeaderHeight - BadgeSize) / 2, BadgeSize, BadgeSize),
                Letter = letter,
                Fill = brush,
                Brush = UmlBrush.None,
            }, item);
            x += BadgeSize + 8;
        }
        var textLeft = x;
        var textWidth = Math.Max(name.Width, stereoSize.Width);
        var y = r.Y + (layout.HeaderHeight - textHeight) / 2;
        if (stereo.Length > 0)
        {
            scene.Add(style.CenteredText(stereo, textLeft + textWidth / 2, y, small: true, brush: UmlBrush.MutedText), item);
            y += stereoSize.Height;
        }
        scene.Add(style.CenteredText(e.Name, textLeft + textWidth / 2, y, bold: true, italic: IsItalic(e),
            underline: e.Kind == UmlElementKind.Object), item);

        y = r.Y + layout.HeaderHeight;
        foreach (var compartment in layout.Compartments)
        {
            scene.Add(new LinePrim { Points = [new Point(r.X, y), new Point(r.Right, y)], Brush = UmlBrush.Border, Thickness = 1 }, item);
            y += Pad * 0.4;
            foreach (var m in compartment)
            {
                if (m.Kind == UmlMemberKind.Separator)
                {
                    scene.Add(style.CenteredText(m.Text, r.Center.X, y + 1, bold: true, small: true, brush: UmlBrush.MutedText), item);
                    y += layout.LineHeight;
                    continue;
                }
                var mx = r.X + Pad;
                if (layout.HasVisibility)
                {
                    if (m.Visibility != UmlVisibility.None)
                    {
                        var glyph = m.Visibility switch
                        {
                            UmlVisibility.Public => UmlGlyph.VisibilityPublic,
                            UmlVisibility.Private => UmlGlyph.VisibilityPrivate,
                            UmlVisibility.Protected => UmlGlyph.VisibilityProtected,
                            _ => UmlGlyph.VisibilityPackage,
                        };
                        var brush = m.Visibility switch
                        {
                            UmlVisibility.Public => UmlBrush.Public,
                            UmlVisibility.Private => UmlBrush.Private,
                            UmlVisibility.Protected => UmlBrush.Protected,
                            _ => UmlBrush.PackageVisibility,
                        };
                        var center = y + layout.LineHeight / 2 - 1;
                        scene.Add(new GlyphPrim { Glyph = glyph, Rect = new Rect(mx, center - 4, 8, 8), Brush = brush, Fill = m.IsMethod ? brush : UmlBrush.None }, item);
                    }
                    mx += GlyphColumn;
                }
                scene.Add(style.Text(m.Text, new Point(mx, y + 1), italic: m.IsAbstract, underline: m.IsStatic), item);
                y += layout.LineHeight;
            }
            y += Pad * 0.4;
        }
    }

    private static bool IsItalic(UmlElement e) => e.Kind == UmlElementKind.AbstractClass;

    private static bool HasBadge(UmlElement e) => e.Kind is not UmlElementKind.Object;

    private static (string Letter, UmlBrush Brush) Badge(UmlElementKind kind) => kind switch
    {
        UmlElementKind.AbstractClass => ("A", UmlBrush.BadgeAbstract),
        UmlElementKind.Interface => ("I", UmlBrush.BadgeInterface),
        UmlElementKind.Enum => ("E", UmlBrush.BadgeEnum),
        UmlElementKind.Annotation => ("@", UmlBrush.BadgeOther),
        UmlElementKind.Entity => ("E", UmlBrush.BadgeOther),
        UmlElementKind.Metaclass => ("M", UmlBrush.BadgeOther),
        UmlElementKind.Stereotype => ("S", UmlBrush.BadgeOther),
        _ => ("C", UmlBrush.BadgeClass),
    };

    // ---- states ----

    private void PaintState(UmlScene scene, UmlElement e, Rect r, int item)
    {
        scene.Add(new ShapePrim { Shape = UmlShape.RoundedRectangle, Rect = r, Radius = Math.Min(r.Height / 2, 14), Shadow = true }, item);
        var stereo = Stereotypes(e);
        var name = style.Measure(e.Name, bold: true);
        var stereoSize = style.Measure(stereo, small: true);
        var members = e.Members.Where(m => m.Kind != UmlMemberKind.Separator).ToList();
        if (members.Count == 0)
        {
            PaintCenteredName(scene, e, stereo, r, item);
            return;
        }

        var y = r.Y + Pad * 0.8;
        PaintCaption(scene, e, stereo, r.Center.X, y, item);
        y += name.Height + stereoSize.Height + Pad * 0.8;
        scene.Add(new LinePrim { Points = [new Point(r.X, y), new Point(r.Right, y)], Brush = UmlBrush.Border, Thickness = 1 }, item);
        y += Pad * 0.5;
        foreach (var m in members)
        {
            var t = scene.Add(style.Text(m.Text, new Point(r.X + Pad * 1.2, y), small: true, brush: UmlBrush.MutedText), item);
            y += t.Size.Height + 2;
        }
    }

    // ---- text helpers ----

    private string Stereotypes(UmlElement e)
    {
        var implied = e.Kind switch
        {
            UmlElementKind.Metaclass => "metaclass",
            UmlElementKind.Stereotype => "stereotype",
            UmlElementKind.Interface when diagramKind is UmlDiagramKind.Profile => "interface",
            _ => null,
        };
        var list = e.Stereotypes.ToList();
        if (implied is not null && !list.Contains(implied, StringComparer.OrdinalIgnoreCase))
            list.Insert(0, implied);
        return list.Count == 0 ? "" : UmlStyle.Guillemets(list);
    }

    /// <summary>Stereotype (small, muted) and name centered under each other, starting at <paramref name="top"/>.</summary>
    private void PaintCaption(UmlScene scene, UmlElement e, string stereo, double centerX, double top, int item)
    {
        if (stereo.Length > 0)
        {
            var s = scene.Add(style.CenteredText(stereo, centerX, top, small: true, brush: UmlBrush.MutedText), item);
            top += s.Size.Height;
        }
        if (e.Name.Length > 0)
            scene.Add(style.CenteredText(e.Name, centerX, top, bold: true), item);
    }

    private void PaintCenteredName(UmlScene scene, UmlElement e, string stereo, Rect r, int item)
    {
        var height = style.Measure(e.Name, bold: true).Height + style.Measure(stereo, small: true).Height;
        PaintCaption(scene, e, stereo, r.Center.X, r.Center.Y - height / 2, item);
    }
}
