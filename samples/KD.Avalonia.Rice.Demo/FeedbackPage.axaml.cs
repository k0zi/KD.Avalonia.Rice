using Avalonia.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class FeedbackPage : UserControl
{
    public FeedbackPage()
    {
        InitializeComponent();
        ReopenButton.Click += (_, _) =>
        {
            foreach (var bar in new[] { InfoBar1, InfoBar2, InfoBar3, InfoBar4, InfoBar5 })
                bar.IsOpen = true;
        };
        LoadedCheck.IsCheckedChanged += (_, _) =>
        {
            var loaded = LoadedCheck.IsChecked == true;
            SkeletonCard.IsVisible = !loaded;
            LoadedCard.IsVisible = loaded;
        };
    }
}
