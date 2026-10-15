using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Chat view: the messages (<see cref="ItemsSource"/>, usually <see cref="RiceChatMessage"/>s) on top,
/// the input box at the bottom. <see cref="ChatStyle"/> switches between bubbles and an IRC like log.
/// Enter sends (<see cref="MessageSent"/>, <see cref="SendCommand"/>), Shift+Enter starts a new line.
/// The control does not add the sent message itself: the app owns the collection.
/// </summary>
public class RiceChat : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<RiceChat, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<RiceChatStyle> ChatStyleProperty =
        AvaloniaProperty.Register<RiceChat, RiceChatStyle>(nameof(ChatStyle));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<RiceChat, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<RiceChat, string?>(nameof(PlaceholderText));

    public static readonly StyledProperty<string> TimestampFormatProperty =
        AvaloniaProperty.Register<RiceChat, string>(nameof(TimestampFormat), "HH:mm");

    public static readonly StyledProperty<ICommand?> SendCommandProperty =
        AvaloniaProperty.Register<RiceChat, ICommand?>(nameof(SendCommand));

    public static readonly StyledProperty<bool> ShowInputProperty =
        AvaloniaProperty.Register<RiceChat, bool>(nameof(ShowInput), true);

    public static readonly StyledProperty<bool> AutoScrollProperty =
        AvaloniaProperty.Register<RiceChat, bool>(nameof(AutoScroll), true);

    public static readonly RoutedEvent<RiceChatMessageSentEventArgs> MessageSentEvent =
        RoutedEvent.Register<RiceChat, RiceChatMessageSentEventArgs>(nameof(MessageSent), RoutingStrategies.Bubble);

    // Distance from the bottom (px) that still counts as "at the bottom" for auto scrolling.
    private const double BottomTolerance = 24;

    private TextBox? _textBox;
    private Button? _sendButton;
    private ScrollViewer? _scrollViewer;
    private INotifyCollectionChanged? _observed;

    public RiceChat()
    {
        // Style priority: read at runtime so RiceStrings can be localized, and a style or local value still wins.
        SetValue(PlaceholderTextProperty, RiceStrings.ChatPlaceholder, BindingPriority.Style);
        UpdatePseudoClasses();
    }

    /// <summary>The messages, typically an <c>ObservableCollection&lt;RiceChatMessage&gt;</c>.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public RiceChatStyle ChatStyle
    {
        get => GetValue(ChatStyleProperty);
        set => SetValue(ChatStyleProperty, value);
    }

    /// <summary>The text being typed.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Format of the message times (local time), e.g. <c>HH:mm</c> or <c>HH:mm:ss</c>.</summary>
    public string TimestampFormat
    {
        get => GetValue(TimestampFormatProperty);
        set => SetValue(TimestampFormatProperty, value);
    }

    /// <summary>Executed on send with the text as parameter.</summary>
    public ICommand? SendCommand
    {
        get => GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }

    /// <summary>False hides the input box, e.g. for a read-only log.</summary>
    public bool ShowInput
    {
        get => GetValue(ShowInputProperty);
        set => SetValue(ShowInputProperty, value);
    }

    /// <summary>Scrolls to new messages when the view is at the bottom (or the message is own).</summary>
    public bool AutoScroll
    {
        get => GetValue(AutoScrollProperty);
        set => SetValue(AutoScrollProperty, value);
    }

    public event EventHandler<RiceChatMessageSentEventArgs>? MessageSent
    {
        add => AddHandler(MessageSentEvent, value);
        remove => RemoveHandler(MessageSentEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceChat);

    /// <summary>Sends the current text (same as pressing Enter). Empty or whitespace text is ignored.</summary>
    public void Send()
    {
        var text = Text?.Trim();
        if (string.IsNullOrEmpty(text))
            return;
        if (SendCommand is { } command && !command.CanExecute(text))
            return;

        RaiseEvent(new RiceChatMessageSentEventArgs(MessageSentEvent, text));
        SendCommand?.Execute(text);
        SetCurrentValue(TextProperty, null);
        _textBox?.Focus();
    }

    public void ScrollToEnd() => _scrollViewer?.ScrollToEnd();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _textBox?.RemoveHandler(KeyDownEvent, OnTextBoxKeyDown);
        _sendButton?.Click -= OnSendButtonClick;

        _textBox = e.NameScope.Find<TextBox>("PART_TextBox");
        _sendButton = e.NameScope.Find<Button>("PART_SendButton");
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");

        // Tunnel: the TextBox (AcceptsReturn) would insert a new line on Enter otherwise.
        _textBox?.AddHandler(KeyDownEvent, OnTextBoxKeyDown, RoutingStrategies.Tunnel);
        if (_sendButton is not null)
        {
            _sendButton.Click += OnSendButtonClick;
            ToolTip.SetTip(_sendButton, RiceStrings.Send);
        }

        ScrollToEndLater();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(ItemsSource);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChatStyleProperty)
        {
            UpdatePseudoClasses();
        }
        else if (change.Property == ItemsSourceProperty)
        {
            if (VisualRoot is not null)
                Observe(ItemsSource);
            ScrollToEndLater();
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":bubbles", ChatStyle == RiceChatStyle.Bubbles);
        PseudoClasses.Set(":classic", ChatStyle == RiceChatStyle.Classic);
    }

    private void Observe(IEnumerable? items)
    {
        _observed?.CollectionChanged -= OnItemsChanged;
        _observed = items as INotifyCollectionChanged;
        _observed?.CollectionChanged += OnItemsChanged;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!AutoScroll || _scrollViewer is not { } sv)
            return;

        // Checked before layout, so the extent is still the old one.
        var atBottom = sv.Offset.Y + sv.Viewport.Height >= sv.Extent.Height - BottomTolerance;
        var ownAdded = e.Action == NotifyCollectionChangedAction.Add
                       && e.NewItems?.OfType<RiceChatMessage>().Any(m => m.IsOwn) == true;
        if (atBottom || ownAdded || e.Action == NotifyCollectionChangedAction.Reset)
            ScrollToEndLater();
    }

    // After the next layout pass, when the new items are measured.
    private void ScrollToEndLater() => Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            Send();
            e.Handled = true;
        }
    }

    private void OnSendButtonClick(object? sender, RoutedEventArgs e) => Send();
}

public class RiceChatMessageSentEventArgs(RoutedEvent routedEvent, string text) : RoutedEventArgs(routedEvent)
{
    /// <summary>The sent text, trimmed.</summary>
    public string Text { get; } = text;
}
