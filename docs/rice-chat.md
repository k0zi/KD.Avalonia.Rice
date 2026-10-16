# RiceChat

Chat view: the messages on top, an input box at the bottom. Two looks: modern bubbles or an IRC like log.

```xml
<rice:RiceChat ItemsSource="{Binding Messages}" ChatStyle="Bubbles" MessageSent="OnMessageSent" />
```

```csharp
public ObservableCollection<RiceChatMessage> Messages { get; } =
[
    new() { Author = "tux", Text = "Welcome to #rice! 🐧" },
];

private void OnMessageSent(object? sender, RiceChatMessageSentEventArgs e) =>
    Messages.Add(new RiceChatMessage { Author = "me", Text = e.Text, IsOwn = true });
```

**The control does not add the sent message itself** – the app owns the collection (e.g. it sends the text to a server
and adds the message when it is confirmed).

## Messages (`RiceChatMessage`)

| Property | Description |
|----------|-------------|
| `Author` | Sender name. |
| `Text` | Message text; long text wraps, new lines are kept. |
| `Timestamp` | `DateTimeOffset`, defaults to now; shown in local time with `TimestampFormat`. |
| `IsOwn` | Written by the local user: on the right in accent color (bubbles) or highlighted (classic). |

`ItemsSource` takes any `IEnumerable` of `RiceChatMessage`s; use an `ObservableCollection` so new messages appear.

## Sending

- Enter sends, Shift+Enter starts a new line. Empty or whitespace-only text is not sent; the text is trimmed.
- On send, the `MessageSent` event is raised (`e.Text`), then `SendCommand` is executed with the text as parameter,
  and the input is cleared and focused again.
- If `SendCommand.CanExecute(text)` is `false`, nothing is sent (the text stays in the box).
- `Text` is the draft (two-way bindable), `Send()` sends it from code.

MVVM with a command (CommunityToolkit.Mvvm):

```xml
<rice:RiceChat ItemsSource="{Binding Messages}" SendCommand="{Binding SendCommand}" Text="{Binding Draft}" />
```

```csharp
[RelayCommand(CanExecute = nameof(IsConnected))]
private async Task Send(string text)
{
    await _client.SendAsync(text);
    Messages.Add(new RiceChatMessage { Author = UserName, Text = text, IsOwn = true });
}
```

## Styles

| `ChatStyle` | Look |
|-------------|------|
| `Bubbles` (default) | Bubbles with the author (others only) and the time; own messages on the right in the accent color, max. 480 px wide. |
| `Classic` | One line per message, `[12:34] <author> text`, monospace font, own author highlighted. |

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `ItemsSource` | `null` | The messages. |
| `ChatStyle` | `Bubbles` | `Bubbles` or `Classic`. |
| `Text` | `null` | The text being typed (two-way). |
| `PlaceholderText` | `RiceStrings.ChatPlaceholder` | Watermark of the input box. |
| `TimestampFormat` | `HH:mm` | Time format, e.g. `HH:mm:ss` or `yyyy-MM-dd HH:mm`. |
| `SendCommand` | `null` | Executed on send with the text as parameter. |
| `ShowInput` | `true` | `false` hides the input bar, e.g. for a read-only log. |
| `AutoScroll` | `true` | Scrolls to new messages while the view is at the bottom, and always for own messages. |
| `Padding` | `12,8` | Around the messages and the input bar. |

Event: `MessageSent` (`RiceChatMessageSentEventArgs.Text`). Methods: `Send()`, `ScrollToEnd()`.

## Read-only log

```xml
<rice:RiceChat ItemsSource="{Binding LogLines}" ChatStyle="Classic" ShowInput="False" TimestampFormat="HH:mm:ss" />
```

## Styling

Brushes: `RiceChatBubbleBrush` (others' bubbles), `RiceChatOwnBubbleBrush` (own bubbles, the accent color),
`RiceChatOwnSelectionBrush` (text selection in own bubbles). Item templates: the `RiceChatBubbleTemplate` and
`RiceChatClassicTemplate` resources. Pseudo classes: `:bubbles`, `:classic`. Template parts: `PART_ScrollViewer`,
`PART_ItemsControl`, `PART_InputBar`, `PART_TextBox`, `PART_SendButton`.
Texts: `RiceStrings.ChatPlaceholder`, `RiceStrings.Send` (send button tool tip).
