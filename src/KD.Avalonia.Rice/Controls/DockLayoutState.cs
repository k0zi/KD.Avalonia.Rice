using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Layout;

namespace KD.Avalonia.Rice.Controls;

/// <summary>A saved <see cref="RiceDockLayout"/>: the tree of splits and tab groups, the auto hidden, floating and closed panes.</summary>
public sealed class RiceDockLayoutState
{
    public int Version { get; init; } = 2;

    public RiceDockNodeState? Root { get; init; }

    public List<RiceDockAutoHideState> AutoHidden { get; init; } = [];

    public List<RiceDockFloatingState> Floating { get; init; } = [];

    public List<string> Closed { get; init; } = [];

    /// <summary>The preferred size and auto hidden state of every pane (all a version 1 layout had).</summary>
    public Dictionary<string, RiceDockPaneState> Panes { get; init; } = [];
}

public sealed record RiceDockPaneState(double Size, bool IsCollapsed);

/// <summary>A split (<see cref="Children"/>) or a tab group (<see cref="PaneIds"/>).</summary>
public sealed class RiceDockNodeState
{
    public bool IsSplit { get; init; }

    public bool IsHorizontal { get; init; }

    public double Size { get; init; } = 1;

    public bool IsStar { get; init; } = true;

    public List<RiceDockNodeState>? Children { get; init; }

    public bool IsDocument { get; init; }

    public List<string>? PaneIds { get; init; }

    public string? Selected { get; init; }
}

public sealed record RiceDockAutoHideState(string Id, DockSide Side);

public sealed class RiceDockFloatingState
{
    public int X { get; init; }

    public int Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public List<string> PaneIds { get; init; } = [];

    public string? Selected { get; init; }
}

internal static class DockStateConverter
{
    public static RiceDockNodeState ToState(DockNode node) => node switch
    {
        DockSplitNode split => new RiceDockNodeState
        {
            IsSplit = true,
            IsHorizontal = split.Orientation == Orientation.Horizontal,
            Size = Math.Round(split.Size.Value, 3),
            IsStar = split.Size.IsStar,
            Children = split.Children.Select(ToState).ToList(),
        },
        DockGroupNode group => new RiceDockNodeState
        {
            Size = Math.Round(group.Size.Value, 3),
            IsStar = group.Size.IsStar,
            IsDocument = group.IsDocument,
            PaneIds = group.Panes.Select(p => p.LayoutId).OfType<string>().ToList(),
            Selected = group.Selected?.LayoutId,
        },
        _ => throw new ArgumentException("Unknown node.", nameof(node)),
    };

    /// <summary>
    /// The tree of a saved state with the panes <paramref name="find"/> knows; unknown panes are skipped, tool groups
    /// left empty are dropped, splits left with one child dissolve. Null when nothing usable is left.
    /// </summary>
    public static DockNode? FromState(RiceDockNodeState state, Func<string, RiceDockPane?> find, ISet<RiceDockPane> used)
    {
        var size = new DockLength(double.IsFinite(state.Size) && state.Size > 0 ? state.Size : 1, state.IsStar);
        if (state.IsSplit)
        {
            var children = (state.Children ?? []).Select(c => FromState(c, find, used)).OfType<DockNode>().ToList();
            if (children.Count == 0)
                return null;
            if (children.Count == 1)
            {
                children[0].Size = size;
                return children[0];
            }
            var split = new DockSplitNode(state.IsHorizontal ? Orientation.Horizontal : Orientation.Vertical) { Size = size };
            foreach (var child in children)
                split.Insert(split.Children.Count, child);
            return split;
        }
        var group = new DockGroupNode(state.IsDocument) { Size = size };
        foreach (var id in state.PaneIds ?? [])
        {
            if (find(id) is { } pane && used.Add(pane))
                group.Panes.Add(pane);
        }
        if (group.Panes.Count == 0 && !group.IsDocument)
            return null;
        group.Selected = group.Panes.FirstOrDefault(p => p.LayoutId == state.Selected) ?? group.Panes.FirstOrDefault();
        return group;
    }
}

/// <summary>Persists a dock layout to <c>$XDG_CONFIG_HOME/&lt;appId&gt;/layouts/&lt;key&gt;.json</c>.</summary>
internal sealed class DockLayoutStore(string appId, string key)
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appId, "layouts", SafeFileName(key) + ".json");

    private static string SafeFileName(string key) =>
        string.Concat(key.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' ? '_' : c));

    public RiceDockLayoutState? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;
            using var stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize(stream, DockLayoutContext.Default.RiceDockLayoutState);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(RiceDockLayoutState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var stream = File.Create(_path);
            JsonSerializer.Serialize(stream, state, DockLayoutContext.Default.RiceDockLayoutState);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing the layout is not worth crashing the app.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RiceDockLayoutState))]
internal sealed partial class DockLayoutContext : JsonSerializerContext;
