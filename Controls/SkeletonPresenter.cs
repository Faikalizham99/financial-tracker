namespace FinancialTracker.Controls;

public sealed class SkeletonPresenter : ContentView
{
    private const uint PulseHalfDuration = 700;
    private const double DimmedOpacity = 0.64;

    private CancellationTokenSource? pulseCancellation;
    private int pulseVersion;

    public SkeletonPresenter()
    {
        InputTransparent = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == IsVisibleProperty.PropertyName)
        {
            UpdatePulseState();
        }
        else if (propertyName == ContentProperty.PropertyName)
        {
            StopPulse();
            UpdatePulseState();
        }
    }

    private void OnLoaded(object? sender, EventArgs e) => UpdatePulseState();

    private void OnUnloaded(object? sender, EventArgs e) => StopPulse();

    private void UpdatePulseState()
    {
        if (IsLoaded && IsVisible)
        {
            StartPulse();
        }
        else
        {
            StopPulse();
        }
    }

    private void StartPulse()
    {
        if (pulseCancellation is not null || Content is not VisualElement content)
        {
            return;
        }

        content.Opacity = 1;
        pulseCancellation = new CancellationTokenSource();
        var version = ++pulseVersion;
        _ = PulseAsync(content, version, pulseCancellation.Token);
    }

    private async Task PulseAsync(
        VisualElement content,
        int version,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && version == pulseVersion)
        {
            await content.FadeToAsync(DimmedOpacity, PulseHalfDuration, Easing.SinInOut);
            if (cancellationToken.IsCancellationRequested || version != pulseVersion)
            {
                break;
            }

            await content.FadeToAsync(1, PulseHalfDuration, Easing.SinInOut);
        }
    }

    private void StopPulse()
    {
        ++pulseVersion;
        pulseCancellation?.Cancel();
        pulseCancellation?.Dispose();
        pulseCancellation = null;
        if (Content is VisualElement content)
        {
            content.CancelAnimations();
            content.Opacity = 1;
        }
    }
}
