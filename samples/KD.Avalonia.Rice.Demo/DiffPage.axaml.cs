using Avalonia.Controls;
using Avalonia.Input.Platform;
using KD.Avalonia.Rice.Controls.Diffs;
using KD.Avalonia.Rice.Notifications;

namespace KD.Avalonia.Rice.Demo;

public partial class DiffPage : UserControl
{
    private const string OldSample = """
        using Avalonia.Controls;

        namespace KD.Avalonia.Rice.Controls;

        /// <summary>Tabs like a browser.</summary>
        public class RiceTabView : TabControl
        {
            private const double DragThreshold = 4;

            public bool ShowAddButton { get; set; }

            public bool CloseTab(int index)
            {
                if (index < 0)
                    return false;
                Items.RemoveAt(index);
                return true;
            }

            private void Move(int from, int to)
            {
                var item = Items[from];
                Items.RemoveAt(from);
                Items.Insert(to, item);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
            }

            // Old helper, no longer used.
            private static int Clamp(int value) => value < 0 ? 0 : value;
        }
        """;

    private const string NewSample = """
        using Avalonia.Controls;
        using Avalonia.Input;

        namespace KD.Avalonia.Rice.Controls;

        /// <summary>Tabs like a browser or an editor, with close buttons.</summary>
        public class RiceTabView : TabControl
        {
            private const double DragThreshold = 6;

            public bool ShowAddButton { get; set; }

            public bool CloseTab(int index)
            {
                if (index < 0 || index >= ItemCount)
                    return false;
                var args = new TabCloseRequestedEventArgs(index);
                RaiseEvent(args);
                if (!args.Cancel)
                    Items.RemoveAt(index);
                return !args.Cancel;
            }

            private void Move(int from, int to)
            {
                var item = Items[from];
                Items.RemoveAt(from);
                Items.Insert(to, item);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control)
                    CloseTab(SelectedIndex);
            }
        }
        """;

    public DiffPage()
    {
        InitializeComponent();
        OldEditor.Text = OldSample;
        NewEditor.Text = NewSample;
        ModeSwitch.SelectionChanged += (_, _) =>
            Diff.ViewMode = ModeSwitch.SelectedIndex == 1 ? DiffViewMode.Inline : DiffViewMode.SideBySide;
        FoldCheck.IsCheckedChanged += (_, _) => Diff.ContextLines = FoldCheck.IsChecked == true ? 3 : -1;
        CopyButton.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                return;
            await clipboard.SetTextAsync(Diff.ToUnifiedDiff());
            RiceToast.ShowSuccess("The unified diff is on the clipboard.");
        };
    }
}
