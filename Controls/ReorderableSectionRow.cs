namespace FinancialTracker.Controls;

public enum VerticalDragStatus
{
    Started,
    Running,
    Completed,
    Canceled
}

public sealed class VerticalDragUpdatedEventArgs(
    VerticalDragStatus status,
    double totalY) : EventArgs
{
    public VerticalDragStatus Status { get; } = status;

    public double TotalY { get; } = totalY;
}

public sealed class ReorderableSectionRow : Grid
{
#if IOS
    private UIKit.UIView? iosPlatformView;
    private UIKit.UIPanGestureRecognizer? iosPanGesture;
#endif

    public ReorderableSectionRow()
    {
#if !WINDOWS && !IOS
        var panGesture = new PanGestureRecognizer
        {
            TouchPoints = 1
        };
        panGesture.PanUpdated += OnMauiPanUpdated;
        GestureRecognizers.Add(panGesture);
#endif
    }

    public event EventHandler<VerticalDragUpdatedEventArgs>? VerticalDragUpdated;

#if !WINDOWS && !IOS
    private void OnMauiPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        var status = e.StatusType switch
        {
            GestureStatus.Started => VerticalDragStatus.Started,
            GestureStatus.Running => VerticalDragStatus.Running,
            GestureStatus.Completed => VerticalDragStatus.Completed,
            _ => VerticalDragStatus.Canceled
        };

        RaiseVerticalDragUpdated(status, e.TotalY);
    }
#endif

    protected override void OnHandlerChanged()
    {
#if IOS
        DetachIosPanGesture();
#endif

        base.OnHandlerChanged();

#if IOS
        if (Handler?.PlatformView is UIKit.UIView platformView)
        {
            iosPlatformView = platformView;
            iosPlatformView.UserInteractionEnabled = true;
            iosPanGesture = new UIKit.UIPanGestureRecognizer(OnIosPanUpdated)
            {
                MinimumNumberOfTouches = 1,
                MaximumNumberOfTouches = 1,
                CancelsTouchesInView = true
            };
            iosPlatformView.AddGestureRecognizer(iosPanGesture);
        }
#endif
    }

    private void RaiseVerticalDragUpdated(VerticalDragStatus status, double totalY) =>
        VerticalDragUpdated?.Invoke(
            this,
            new VerticalDragUpdatedEventArgs(status, totalY));

#if IOS
    private void OnIosPanUpdated(UIKit.UIPanGestureRecognizer recognizer)
    {
        var totalY = (double)recognizer.TranslationInView(recognizer.View).Y;
        var status = recognizer.State switch
        {
            UIKit.UIGestureRecognizerState.Began => VerticalDragStatus.Started,
            UIKit.UIGestureRecognizerState.Changed => VerticalDragStatus.Running,
            UIKit.UIGestureRecognizerState.Ended => VerticalDragStatus.Completed,
            _ => VerticalDragStatus.Canceled
        };

        RaiseVerticalDragUpdated(status, totalY);
    }

    private void DetachIosPanGesture()
    {
        if (iosPlatformView is not null && iosPanGesture is not null)
        {
            iosPlatformView.RemoveGestureRecognizer(iosPanGesture);
            iosPanGesture.Dispose();
        }

        iosPanGesture = null;
        iosPlatformView = null;
    }
#endif
}
