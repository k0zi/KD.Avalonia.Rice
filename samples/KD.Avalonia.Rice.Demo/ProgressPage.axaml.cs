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

    // Busy for two seconds: the spinner shows and the button ignores clicks, but keeps its colors
    // (a disabled button would turn the spinner gray).
    private void OnBusyButtonClicked(object? sender, RoutedEventArgs e)
    {
        var (button, spinner, text, idle, busy) = sender == SaveButton
            ? (SaveButton, SaveSpinner, SaveText, "Save", "Saving…")
            : (RefreshButton, RefreshSpinner, RefreshText, "Refresh", "Refreshing…");
        if (spinner.IsActive)
            return;

        spinner.IsActive = true;
        text.Text = busy;
        button.IsHitTestVisible = false;
        DispatcherTimer.RunOnce(() =>
        {
            spinner.IsActive = false;
            text.Text = idle;
            button.IsHitTestVisible = true;
        }, TimeSpan.FromSeconds(2));
    }

    // Indeterminate for two seconds, or 0-100 % in about two seconds.
    private void OnOverlayClicked(object? sender, RoutedEventArgs e)
    {
        if (FormOverlay.IsActive)
            return;

        var withProgress = sender == OverlayProgressButton;
        FormOverlay.IsIndeterminate = !withProgress;
        FormOverlay.Value = 0;
        FormOverlay.Message = withProgress ? "Uploading…" : "Loading profile…";
        FormOverlay.IsActive = true;
        if (!withProgress)
        {
            DispatcherTimer.RunOnce(() => FormOverlay.IsActive = false, TimeSpan.FromSeconds(2));
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            FormOverlay.Value += 1;
            if (FormOverlay.Value < 100)
                return;
            timer.Stop();
            FormOverlay.IsActive = false;
        };
        timer.Start();
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
