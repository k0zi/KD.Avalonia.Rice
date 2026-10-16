using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace KD.Avalonia.Rice.Demo;

public partial class ProgressPage : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public ProgressPage()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
    }

    // "Preparing" with unknown length first, then a determinate phase.
    private void OnStartClicked(object? sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        WorkRing.Value = 0;
        WorkRing.IsIndeterminate = true;
        WorkStatus.Text = "Preparing…";
        DispatcherTimer.RunOnce(() =>
        {
            WorkRing.IsIndeterminate = false;
            WorkStatus.Text = "Downloading…";
            _timer.Start();
        }, TimeSpan.FromSeconds(2));
    }

    private void OnTick(object? sender, EventArgs e)
    {
        WorkRing.Value += 1;
        if (WorkRing.Value < WorkRing.Maximum)
            return;

        _timer.Stop();
        WorkStatus.Text = "Done";
        StartButton.IsEnabled = true;
    }
}
