namespace KD.Avalonia.Rice.Controls;

/// <summary>Look of a <see cref="RiceChat"/>.</summary>
public enum RiceChatStyle
{
    /// <summary>Modern chat: messages in bubbles, own messages on the right.</summary>
    Bubbles,

    /// <summary>IRC like: one line per message, <c>[time] &lt;author&gt; text</c>.</summary>
    Classic,
}
