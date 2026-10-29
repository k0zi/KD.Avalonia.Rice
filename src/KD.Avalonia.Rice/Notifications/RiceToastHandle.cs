using Avalonia.Threading;

namespace KD.Avalonia.Rice.Notifications;

/// <summary>A shown toast; closes it from code. Can be used from any thread.</summary>
public sealed class RiceToastHandle
{
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RiceToastItem? _item;
    private bool _closeRequested;

    /// <summary>Completes when the toast is closed (or could not be shown: no window).</summary>
    public Task Closed => _closed.Task;

    public bool IsClosed => _closed.Task.IsCompleted;

    public void Close() => Dispatcher.UIThread.Post(() =>
    {
        if (_item is null)
            _closeRequested = true;
        else
            _item.Close();
    });

    internal void Attach(RiceToastItem item)
    {
        _item = item;
        item.Closed += (_, _) => _closed.TrySetResult();
        if (_closeRequested)
            item.Close();
    }

    internal void SetClosed() => _closed.TrySetResult();
}
