namespace KD.Avalonia.Rice.Controls;

/// <summary>A message shown by <see cref="RiceChat"/>.</summary>
public class RiceChatMessage
{
    public string Author { get; init; } = "";

    public string Text { get; init; } = "";

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    /// <summary>Written by the local user: shown on the right in accent color (bubbles) or highlighted (classic).</summary>
    public bool IsOwn { get; init; }
}
