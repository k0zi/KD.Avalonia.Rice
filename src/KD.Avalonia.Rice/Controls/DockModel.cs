using Avalonia.Layout;

namespace KD.Avalonia.Rice.Controls;

/// <summary>Where a <see cref="RiceDockPane"/> starts in a <see cref="RiceDockLayout"/>.</summary>
public enum DockSide
{
    Left,
    Top,
    Right,
    Bottom,

    /// <summary>A tab of the document area in the middle.</summary>
    Document,
}

/// <summary>Where a pane goes relative to another one (<see cref="RiceDockLayout.AddPane(RiceDockPane, RiceDockPane, DockPosition)"/>).</summary>
public enum DockPosition
{
    Left,
    Top,
    Right,
    Bottom,

    /// <summary>Another tab of the same group.</summary>
    Tab,
}

/// <summary>Size of a node in a split: pixels (tool windows) or a share of the rest (documents).</summary>
internal readonly record struct DockLength(double Value, bool IsStar)
{
    public static DockLength Star(double value = 1) => new(value, true);

    public static DockLength Pixel(double value) => new(value, false);
}

internal abstract class DockNode
{
    public DockSplitNode? Parent { get; set; }

    public DockLength Size { get; set; } = DockLength.Star();

    /// <summary>Part of the document area (only document groups below it).</summary>
    public abstract bool IsDocument { get; }
}

/// <summary>Nodes side by side (<see cref="Orientation.Horizontal"/>) or above each other, with splitters between them.</summary>
internal sealed class DockSplitNode(Orientation orientation) : DockNode
{
    public Orientation Orientation { get; } = orientation;

    public List<DockNode> Children { get; } = [];

    public override bool IsDocument => Children.Count > 0 && Children.All(c => c.IsDocument);

    public void Insert(int index, DockNode child)
    {
        child.Parent = this;
        Children.Insert(Math.Clamp(index, 0, Children.Count), child);
    }
}

/// <summary>Panes shown as tabs, one of them at a time.</summary>
internal sealed class DockGroupNode(bool isDocument) : DockNode
{
    public List<RiceDockPane> Panes { get; } = [];

    public RiceDockPane? Selected { get; set; }

    public override bool IsDocument { get; } = isDocument;
}

/// <summary>
/// The tree of a <see cref="RiceDockLayout"/>: splits of tab groups around the document area, which always has at least
/// one (maybe empty) document group. Tool groups that lose their last pane go away, splits with one child dissolve.
/// </summary>
internal sealed class DockModel
{
    public DockModel(DockNode root)
    {
        Root = root;
        EnsureDocumentGroup();
    }

    public DockNode Root { get; private set; }

    public IEnumerable<DockNode> Nodes => Walk(Root);

    public IEnumerable<DockGroupNode> Groups => Nodes.OfType<DockGroupNode>();

    public IEnumerable<RiceDockPane> Panes => Groups.SelectMany(g => g.Panes);

    /// <summary>The topmost node of the document area.</summary>
    public DockNode DocumentRoot => Nodes.First(n => n.IsDocument);

    /// <summary>The first document group, where new documents go.</summary>
    public DockGroupNode MainDocumentGroup => Groups.First(g => g.IsDocument);

    public DockGroupNode? GroupOf(RiceDockPane pane) => Groups.FirstOrDefault(g => g.Panes.Contains(pane));

    public bool Contains(DockNode node) => Nodes.Contains(node);

    private static IEnumerable<DockNode> Walk(DockNode node)
    {
        yield return node;
        if (node is DockSplitNode split)
        {
            foreach (var child in split.Children.ToList())
            {
                foreach (var n in Walk(child))
                    yield return n;
            }
        }
    }

    /// <summary>
    /// The starting layout of the declared panes, like a <c>DockPanel</c>: the first pane takes
    /// its edge of the whole layout, the next one its edge of the rest, and so on; documents fill the middle. Panes with
    /// the same <see cref="RiceDockPane.Group"/> share a tab group (at the place of the first of them).
    /// </summary>
    public static DockModel Build(IEnumerable<RiceDockPane> panes)
    {
        var list = panes.ToList();
        var documents = new DockGroupNode(true);
        foreach (var pane in list.Where(p => p.Dock == DockSide.Document))
            documents.Panes.Add(pane);
        documents.Selected = documents.Panes.FirstOrDefault();

        var groups = new List<(DockGroupNode Group, DockSide Side)>();
        var byName = new Dictionary<string, DockGroupNode>();
        foreach (var pane in list.Where(p => p.Dock != DockSide.Document))
        {
            if (pane.Group is { } name && byName.TryGetValue(name, out var existing))
            {
                existing.Panes.Add(pane);
                continue;
            }
            var group = new DockGroupNode(false) { Size = DockLength.Pixel(pane.Size), Selected = pane };
            group.Panes.Add(pane);
            groups.Add((group, pane.Dock));
            if (pane.Group is { } key)
                byName[key] = group;
        }

        DockNode root = documents;
        for (var i = groups.Count - 1; i >= 0; i--)
            root = Wrap(root, groups[i].Group, groups[i].Side);
        return new DockModel(root);
    }

    // Puts a group at an edge of a node: into the node when it is a split of that direction, else in a new split.
    private static DockNode Wrap(DockNode inner, DockGroupNode group, DockSide side)
    {
        var orientation = OrientationOf(side);
        var first = side is DockSide.Left or DockSide.Top;
        if (inner is DockSplitNode split && split.Orientation == orientation)
        {
            split.Insert(first ? 0 : split.Children.Count, group);
            return split;
        }
        var wrapper = new DockSplitNode(orientation) { Size = inner.Size };
        inner.Size = DockLength.Star();
        wrapper.Insert(0, inner);
        wrapper.Insert(first ? 0 : 1, group);
        return wrapper;
    }

    private static Orientation OrientationOf(DockSide side) =>
        side is DockSide.Left or DockSide.Right ? Orientation.Horizontal : Orientation.Vertical;

    private static Orientation OrientationOf(DockPosition position) =>
        position is DockPosition.Left or DockPosition.Right ? Orientation.Horizontal : Orientation.Vertical;

    public static void AddToGroup(DockGroupNode group, IEnumerable<RiceDockPane> panes, int index = -1)
    {
        var added = panes.ToList();
        var at = index < 0 ? group.Panes.Count : Math.Min(index, group.Panes.Count);
        group.Panes.InsertRange(at, added);
        group.Selected = added.FirstOrDefault() ?? group.Selected;
    }

    /// <summary>Takes a pane out of its group; a tool group left empty goes away (the last document group stays).</summary>
    public void Remove(RiceDockPane pane)
    {
        if (GroupOf(pane) is not { } group)
            return;
        var index = group.Panes.IndexOf(pane);
        group.Panes.RemoveAt(index);
        if (group.Selected == pane)
            group.Selected = group.Panes.Count > 0 ? group.Panes[Math.Min(index, group.Panes.Count - 1)] : null;
        if (group.Panes.Count == 0 && !(group.IsDocument && Groups.Count(g => g.IsDocument) == 1))
            RemoveNode(group);
    }

    private void RemoveNode(DockNode node)
    {
        if (node.Parent is not { } parent)
            return;
        parent.Children.Remove(node);
        node.Parent = null;
        if (parent.Children.Count == 1)
        {
            var only = parent.Children[0];
            only.Size = parent.Size;
            Replace(parent, only);
        }
    }

    private void Replace(DockNode old, DockNode replacement)
    {
        var parent = old.Parent;
        replacement.Parent = parent;
        if (parent is null)
            Root = replacement;
        else
            parent.Children[parent.Children.IndexOf(old)] = replacement;
        old.Parent = null;
    }

    /// <summary>Docks panes as a new group at an edge of the whole layout (or as documents).</summary>
    public DockGroupNode DockToEdge(IReadOnlyList<RiceDockPane> panes, DockSide side, double size)
    {
        if (side == DockSide.Document)
        {
            AddToGroup(MainDocumentGroup, panes);
            return MainDocumentGroup;
        }
        var group = new DockGroupNode(false) { Size = DockLength.Pixel(size) };
        AddToGroup(group, panes);
        var root = Root;
        Root = Wrap(root, group, side);
        Root.Parent = null;
        return group;
    }

    /// <summary>
    /// Docks panes next to <paramref name="target"/> (a group, or the document area as a whole) or into it as tabs.
    /// A tool group next to the document area takes <paramref name="size"/> pixels; otherwise the target is halved.
    /// </summary>
    public DockGroupNode DockNextTo(IReadOnlyList<RiceDockPane> panes, DockNode target, DockPosition position, bool asDocuments, double size)
    {
        if (position == DockPosition.Tab && target is DockGroupNode tabs)
        {
            AddToGroup(tabs, panes);
            return tabs;
        }
        var group = new DockGroupNode(asDocuments);
        AddToGroup(group, panes);
        var orientation = OrientationOf(position);
        var before = position is DockPosition.Left or DockPosition.Top;
        var fixedSize = !asDocuments && target.IsDocument;
        if (target.Parent is { } parent && parent.Orientation == orientation)
        {
            var index = parent.Children.IndexOf(target);
            if (fixedSize)
                group.Size = DockLength.Pixel(size);
            else
            {
                var half = target.Size with { Value = target.Size.Value / 2 };
                target.Size = half;
                group.Size = half;
            }
            parent.Insert(before ? index : index + 1, group);
            return group;
        }
        var split = new DockSplitNode(orientation) { Size = target.Size };
        Replace(target, split);
        target.Size = DockLength.Star();
        group.Size = fixedSize ? DockLength.Pixel(size) : DockLength.Star();
        split.Insert(0, target);
        split.Insert(before ? 0 : 1, group);
        return group;
    }

    // There is always a place for documents, even when the saved or built tree has none.
    private void EnsureDocumentGroup()
    {
        if (Groups.Any(g => g.IsDocument))
            return;
        var documents = new DockGroupNode(true);
        var split = new DockSplitNode(Orientation.Horizontal);
        var root = Root;
        root.Size = DockLength.Pixel(Math.Max(120, root.Size.IsStar ? 240 : root.Size.Value));
        split.Insert(0, root);
        split.Insert(1, documents);
        Root = split;
    }

    /// <summary>
    /// The lengths of the children of a split in <paramref name="total"/>: pixels first, the rest shared by the stars.
    /// When the stars would get less than <paramref name="minLength"/> each, the pixel sizes shrink; without stars the
    /// pixel sizes are scaled to fill.
    /// </summary>
    public static double[] Lengths(IReadOnlyList<DockLength> sizes, double total, double minLength)
    {
        var lengths = new double[sizes.Count];
        total = Math.Max(0, total);
        var pixelSum = sizes.Where(s => !s.IsStar).Sum(s => Math.Max(0, s.Value));
        var starSum = sizes.Where(s => s.IsStar).Sum(s => Math.Max(0, s.Value));
        var starCount = sizes.Count(s => s.IsStar);
        var scale = 1.0;
        if (starCount == 0)
            scale = pixelSum > 0 ? total / pixelSum : 0;
        else if (total - pixelSum < minLength * starCount)
            scale = pixelSum > 0 ? Math.Max(0, total - minLength * starCount) / pixelSum : 0;
        var starSpace = Math.Max(0, total - pixelSum * scale);
        for (var i = 0; i < sizes.Count; i++)
        {
            var size = sizes[i];
            lengths[i] = size.IsStar
                ? starSum > 0 ? starSpace * Math.Max(0, size.Value) / starSum : starSpace / starCount
                : Math.Max(0, size.Value) * scale;
        }
        return lengths;
    }
}
