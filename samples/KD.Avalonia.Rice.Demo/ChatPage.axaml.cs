using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class ChatPage : UserControl
{
    private static readonly string[] Replies =
    [
        "Have you tried turning it off and on again?",
        "Works on my machine.",
        "I use Arch, btw.",
        "Sounds good, ship it! 🚀",
        "Did you read the man page?",
    ];

    private readonly ObservableCollection<RiceChatMessage> _messages =
    [
        new() { Author = "tux", Text = "Welcome to #rice! 🐧", Timestamp = DateTimeOffset.Now.AddMinutes(-12) },
        new() { Author = "ferris", Text = "Anyone tried the new Nord theme?", Timestamp = DateTimeOffset.Now.AddMinutes(-9) },
        new() { Author = "me", Text = "Yes, it looks great in dark mode.", Timestamp = DateTimeOffset.Now.AddMinutes(-8), IsOwn = true },
        new() { Author = "tux", Text = "Switch to the classic style above for the IRC look.\nLong messages wrap, Shift+Enter starts a new line.", Timestamp = DateTimeOffset.Now.AddMinutes(-2) },
    ];

    private int _replyIndex;

    public ChatPage()
    {
        InitializeComponent();
        Chat.ItemsSource = _messages;
    }

    private void OnBubblesClicked(object? sender, RoutedEventArgs e) => Chat.ChatStyle = RiceChatStyle.Bubbles;

    private void OnClassicClicked(object? sender, RoutedEventArgs e) => Chat.ChatStyle = RiceChatStyle.Classic;

    // The chat does not add the message itself; a fake reply follows to show both sides.
    private void OnMessageSent(object? sender, RiceChatMessageSentEventArgs e)
    {
        _messages.Add(new RiceChatMessage { Author = "me", Text = e.Text, IsOwn = true });
        var reply = Replies[_replyIndex++ % Replies.Length];
        DispatcherTimer.RunOnce(() => _messages.Add(new RiceChatMessage { Author = "tux", Text = reply }),
            TimeSpan.FromSeconds(1));
    }
}
