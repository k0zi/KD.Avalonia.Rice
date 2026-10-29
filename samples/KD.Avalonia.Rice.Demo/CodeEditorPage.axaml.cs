using Avalonia.Controls;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class CodeEditorPage : UserControl
{
    internal static readonly Dictionary<string, string> Samples = new()
    {
        ["cs"] = """
            using Avalonia.Controls;

            namespace Demo;

            // A tiny view model: Ctrl+/ toggles this comment.
            public sealed class Counter
            {
                private int _count;

                public string Text => $"Clicked {_count} times";

                public void Increment()
                {
                    if (_count < 100)
                        _count++;
                }
            }
            """,
        ["xaml"] = """
            <Window xmlns="https://github.com/avaloniaui"
                    Title="Hello" Width="400" Height="200">
              <!-- A button in the middle -->
              <Button Content="Click me" HorizontalAlignment="Center" />
            </Window>
            """,
        ["json"] = """
            {
              "themeId": "nord",
              "panes": { "explorer": { "size": 260, "isCollapsed": false } },
              "recent": [ "README.md", "App.axaml" ],
              "fontSize": 13
            }
            """,
        ["py"] = """
            # Fibonacci numbers
            def fib(n: int) -> list[int]:
                result = [0, 1]
                while len(result) < n:
                    result.append(result[-1] + result[-2])
                return result[:n]

            print(fib(10))
            """,
        ["sql"] = """
            -- Open tasks per column
            SELECT c.title, COUNT(t.id) AS open_tasks
            FROM columns c
            LEFT JOIN tasks t ON t.column_id = c.id AND t.done = FALSE
            GROUP BY c.title
            ORDER BY open_tasks DESC;
            """,
    };

    // Each language keeps its own text while switching.
    private readonly Dictionary<string, string> _texts = new(Samples);
    private string _language = "cs";

    public CodeEditorPage()
    {
        InitializeComponent();
        Editor.Language = _language;
        Editor.Text = _texts[_language];
        LanguageSwitch.SelectionChanged += (_, _) =>
        {
            if (LanguageSwitch.SelectedItem is not RiceSegmentedItem { Tag: string language })
                return;
            _texts[_language] = Editor.Text ?? "";
            _language = language;
            Editor.Language = language;
            Editor.Text = _texts[language];
        };
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.TextProperty)
                UpdateCaretInfo();
        };
        UpdateCaretInfo();
    }

    private void UpdateCaretInfo()
    {
        var text = Editor.Text ?? "";
        var caret = Math.Clamp(Editor.CaretIndex, 0, text.Length);
        var lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var line = text.AsSpan(0, caret).Count('\n') + 1;
        CaretInfo.Text = $"Ln {line}, Col {caret - lineStart + 1}   ·   {_language}   ·   {text.AsSpan().Count('\n') + 1} lines";
    }
}
