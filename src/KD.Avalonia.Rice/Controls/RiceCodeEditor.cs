using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Light code editor: a <see cref="TextBox"/> with syntax colored text (the languages of the Markdown code blocks:
/// <c>cs</c>, <c>xaml</c>, <c>json</c>, <c>js</c>, <c>py</c>, <c>bash</c>, <c>sql</c>, <c>yaml</c>, <c>plantuml</c>,
/// <c>mermaid</c>), line numbers, the caret's line highlighted, and editor keys: Tab / Shift+Tab indent and outdent
/// (the selected lines too), Enter keeps the indentation, Backspace in the indentation goes back a tab stop, Ctrl+/
/// comments the lines out. Bind <c>Text</c> as usual; every command is one undo step (Ctrl+Z).
/// Meant for snippets, configuration and scripts up to a few thousand lines: the whole text is colored again on every change.
/// </summary>
public class RiceCodeEditor : TextBox
{
    public static readonly StyledProperty<string?> LanguageProperty =
        RiceCodeTextPresenter.LanguageProperty.AddOwner<RiceCodeEditor>();

    public static readonly StyledProperty<bool> ShowLineNumbersProperty =
        AvaloniaProperty.Register<RiceCodeEditor, bool>(nameof(ShowLineNumbers), true);

    public static readonly StyledProperty<bool> HighlightCurrentLineProperty =
        AvaloniaProperty.Register<RiceCodeEditor, bool>(nameof(HighlightCurrentLine), true);

    public static readonly StyledProperty<int> TabSizeProperty =
        AvaloniaProperty.Register<RiceCodeEditor, int>(nameof(TabSize), 4, coerce: (_, value) => Math.Clamp(value, 1, 16));

    public static readonly StyledProperty<bool> ConvertTabsToSpacesProperty =
        AvaloniaProperty.Register<RiceCodeEditor, bool>(nameof(ConvertTabsToSpaces), true);

    public static readonly StyledProperty<bool> AutoIndentProperty =
        AvaloniaProperty.Register<RiceCodeEditor, bool>(nameof(AutoIndent), true);

    /// <summary>Overrides the line comment of the language (e.g. <c>;</c> for an INI file).</summary>
    public static readonly StyledProperty<string?> LineCommentProperty =
        AvaloniaProperty.Register<RiceCodeEditor, string?>(nameof(LineComment));

    private RiceCodeGutter? _gutter;
    private RiceCodeLineHighlight? _lineHighlight;
    private ScrollViewer? _scrollViewer;

    /// <summary>The language of the code, as in a Markdown info string; null or unknown: plain text.</summary>
    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public bool ShowLineNumbers
    {
        get => GetValue(ShowLineNumbersProperty);
        set => SetValue(ShowLineNumbersProperty, value);
    }

    public bool HighlightCurrentLine
    {
        get => GetValue(HighlightCurrentLineProperty);
        set => SetValue(HighlightCurrentLineProperty, value);
    }

    /// <summary>Width of a tab stop in spaces (1–16). Default 4.</summary>
    public int TabSize
    {
        get => GetValue(TabSizeProperty);
        set => SetValue(TabSizeProperty, value);
    }

    /// <summary>Tab inserts spaces up to the next tab stop instead of a tab character. Default true.</summary>
    public bool ConvertTabsToSpaces
    {
        get => GetValue(ConvertTabsToSpacesProperty);
        set => SetValue(ConvertTabsToSpacesProperty, value);
    }

    /// <summary>Enter keeps the indentation of the line (one more after <c>{ [ (</c>). Default true.</summary>
    public bool AutoIndent
    {
        get => GetValue(AutoIndentProperty);
        set => SetValue(AutoIndentProperty, value);
    }

    public string? LineComment
    {
        get => GetValue(LineCommentProperty);
        set => SetValue(LineCommentProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceCodeEditor);

    private string IndentUnit => ConvertTabsToSpaces ? new string(' ', TabSize) : "\t";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var presenter = e.NameScope.Find<RiceCodeTextPresenter>("PART_TextPresenter");
        _gutter?.Presenter = null;
        _lineHighlight?.Presenter = null;
        _scrollViewer?.ScrollChanged -= OnScrollChanged;
        _gutter = e.NameScope.Find<RiceCodeGutter>("PART_Gutter");
        _lineHighlight = e.NameScope.Find<RiceCodeLineHighlight>("PART_LineHighlight");
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _gutter?.Presenter = presenter;
        _lineHighlight?.Presenter = presenter;
        _scrollViewer?.ScrollChanged += OnScrollChanged;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => _gutter?.InvalidateVisual();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsReadOnly && HandleEditorKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private bool HandleEditorKey(Key key, KeyModifiers modifiers)
    {
        var text = Text ?? "";
        var start = SelectionStart;
        var end = SelectionEnd;
        CodeEdit? edit = (key, modifiers) switch
        {
            (Key.Tab, KeyModifiers.None) when CodeEditing.IsMultiLine(text, start, end) => CodeEditing.Indent(text, start, end, IndentUnit),
            (Key.Tab, KeyModifiers.None) => CodeEditing.InsertTab(text, start, end, TabSize, ConvertTabsToSpaces),
            (Key.Tab, KeyModifiers.Shift) => CodeEditing.Outdent(text, start, end, TabSize) ?? NoEdit,
            (Key.Enter, KeyModifiers.None) when AutoIndent && AcceptsReturn => CodeEditing.NewLine(text, start, end, NewLine, IndentUnit),
            (Key.Back, KeyModifiers.None) when start == end && ConvertTabsToSpaces => CodeEditing.Backspace(text, start, TabSize),
            (Key.Oem2 or Key.Divide, KeyModifiers.Control) => ToggleComment(text, start, end),
            _ => null,
        };
        if (edit is not { } change)
            return false;
        if (change != NoEdit)
            Apply(change);
        return true;
    }

    // Handled without a change, e.g. Shift+Tab on a line without indentation (it would move the focus otherwise).
    private static readonly CodeEdit NoEdit = new(-1, 0, "", -1, -1);

    private CodeEdit ToggleComment(string text, int start, int end) =>
        (LineComment ?? CodeEditing.LineCommentPrefix(Language)) is { Length: > 0 } prefix
            ? CodeEditing.ToggleLineComment(text, start, end, prefix) ?? NoEdit
            : NoEdit;

    /// <summary>Applies an edit through the selection, so the text box records it as one undo step.</summary>
    private void Apply(CodeEdit edit)
    {
        // Setting the caret collapses the selection, so it goes first; the end of the selection is its active end.
        CaretIndex = edit.Start;
        SelectionEnd = edit.Start + edit.Length;
        if (edit.Replacement.Length > 0 || edit.Length > 0)
            SelectedText = edit.Replacement;
        CaretIndex = edit.SelectionStart;
        SelectionEnd = edit.SelectionEnd;
    }

    /// <summary>Comments the selected lines out, or removes their comment (Ctrl+/).</summary>
    public void ToggleLineComment()
    {
        if (!IsReadOnly && ToggleComment(Text ?? "", SelectionStart, SelectionEnd) is var edit && edit != NoEdit)
            Apply(edit);
    }
}
