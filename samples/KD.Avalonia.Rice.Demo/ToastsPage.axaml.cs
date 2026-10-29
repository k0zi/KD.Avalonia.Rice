using Avalonia.Controls;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Notifications;

namespace KD.Avalonia.Rice.Demo;

public partial class ToastsPage : UserControl
{
    private static readonly Dictionary<ToastKind, (string Title, string Message)> Samples = new()
    {
        [ToastKind.Info] = ("Update available", "Version 26.11 can be installed at the next start."),
        [ToastKind.Success] = ("Saved", "report.md was saved."),
        [ToastKind.Warning] = ("Low disk space", "Only 2 GB are left on the home partition."),
        [ToastKind.Error] = ("Upload failed", "The server did not respond in 30 seconds."),
    };

    private int _count;

    public ToastsPage()
    {
        InitializeComponent();

        foreach (var (kind, (title, message)) in Samples)
        {
            var button = new Button { Content = kind.ToString() };
            button.Click += (_, _) => Track(RiceToast.Show(new ToastOptions { Kind = kind, Title = title, Message = message }, Owner), kind.ToString());
            KindButtons.Children.Add(button);
        }

        TitleOnlyButton.Click += (_, _) => Track(RiceToast.ShowSuccess($"Copied to the clipboard ({++_count})", owner: Owner), "title only");
        StickyButton.Click += (_, _) => Track(RiceToast.Show(new ToastOptions
        {
            Kind = ToastKind.Warning,
            Title = "Unsaved changes",
            Message = "This toast stays until you close it.",
            Duration = TimeSpan.Zero,
        }, Owner), "sticky");
        ActionButton.Click += (_, _) => Track(RiceToast.ShowInfo("3 files were moved to the trash.", "Deleted",
            new ToastAction("Undo", () => ShowResult("Undo clicked")), Owner), "action");
        ClickableButton.Click += (_, _) => Track(RiceToast.Show(new ToastOptions
        {
            Kind = ToastKind.Info,
            Title = "New message",
            Message = "Click the toast to open it.",
            OnClick = () => ShowResult("toast clicked"),
        }, Owner), "clickable");
        CustomContentButton.Click += (_, _) => Track(RiceToast.Show(new ToastOptions
        {
            Kind = ToastKind.Success,
            Title = "Download complete",
            Content = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = "ubuntu-26.10-desktop-amd64.iso" },
                    new TextBlock { Text = "6.2 GB · 4 min 12 s", Opacity = 0.7, FontSize = 12 },
                },
            },
            Duration = TimeSpan.FromSeconds(10),
        }, Owner), "custom content");
        BackgroundThreadButton.Click += (_, _) => Task.Run(async () =>
        {
            await Task.Delay(500);
            await RiceToast.ShowError("Raised on a worker thread after 0.5 s.", "Background job failed").Closed;
        });

        PositionBox.ItemsSource = Enum.GetValues<ToastPosition>();
        PositionBox.SelectedItem = ToastPosition.BottomRight;
        PositionBox.SelectionChanged += (_, _) =>
        {
            if (Owner is RiceWindow window && PositionBox.SelectedItem is ToastPosition position)
                window.ToastPosition = position;
        };
        CloseAllButton.Click += (_, _) => RiceToast.CloseAll(Owner);
    }

    private TopLevel? Owner => TopLevel.GetTopLevel(this);

    private async void Track(RiceToastHandle toast, string name)
    {
        await toast.Closed;
        ShowResult($"{name} closed");
    }

    private void ShowResult(string result) => ResultText.Text = $"Result: {result}";
}
