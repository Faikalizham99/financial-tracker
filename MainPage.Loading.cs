namespace FinancialTracker;

public partial class MainPage
{
    private void UpdateLoadingSkeletonForSelectedSection()
    {
        if (!isDataLoadingSkeletonShown)
        {
            return;
        }

        var shouldShow = selectedSectionIndex <= 1;
        HomeLoadingSkeleton.IsVisible = selectedSectionIndex == 0;
        TransactionLoadingSkeleton.IsVisible = selectedSectionIndex == 1;
        DataLoadingOverlay.IsVisible = shouldShow;
        DataLoadingOverlay.Opacity = shouldShow ? 1 : 0;

        if (shouldShow)
        {
            StartLoadingSkeletonPulse();
        }
        else
        {
            StopLoadingSkeletonPulse();
        }
    }

    private void StartLoadingSkeletonPulse()
    {
        if (loadingSkeletonPulseCancellation is not null)
        {
            return;
        }

        loadingSkeletonPulseCancellation = new CancellationTokenSource();
        _ = PulseLoadingSkeletonAsync(
            loadingSkeletonPulseCancellation.Token);
    }

    private void ShowTransactionLoadingSkeleton()
    {
        isDataLoadingSkeletonShown = true;
        DataLoadingOverlay.CancelAnimations();
        HomeLoadingSkeleton.IsVisible = false;
        TransactionLoadingSkeleton.IsVisible = true;
        DataLoadingOverlay.Opacity = 1;
        DataLoadingOverlay.IsVisible = true;
        StartLoadingSkeletonPulse();
    }

    private bool ShowDataLoadingSkeletonForSelectedSection()
    {
        isDataLoadingSkeletonShown = true;
        DataLoadingOverlay.CancelAnimations();
        DataLoadingOverlay.ZIndex = 140;
        HomeLoadingSkeleton.IsVisible = selectedSectionIndex == 0;
        TransactionLoadingSkeleton.IsVisible = selectedSectionIndex != 0;
        DataLoadingOverlay.Opacity = 1;
        DataLoadingOverlay.IsVisible = true;
        StartLoadingSkeletonPulse();
        return true;
    }

    private async Task RunWithDataLoadingSkeletonAsync(Func<Task> operation)
    {
        await dataLoadingOperationLock.WaitAsync();
        var isSkeletonVisible = false;

        try
        {
            isSkeletonVisible = ShowDataLoadingSkeletonForSelectedSection();
            if (isSkeletonVisible)
            {
                // Let the loading state reach the native compositor before an
                // in-memory list rebuild or SQLite operation starts.
                await Task.Yield();
            }

            await operation();
        }
        finally
        {
            try
            {
                if (isSkeletonVisible)
                {
                    await HideLoadingSkeletonAsync();
                }
            }
            finally
            {
                DataLoadingOverlay.ZIndex = 20;
                dataLoadingOperationLock.Release();
            }
        }
    }

    private async Task PulseLoadingSkeletonAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await DataLoadingSkeletonPulseLayer.FadeToAsync(
                0.58,
                520,
                Easing.SinInOut);
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await DataLoadingSkeletonPulseLayer.FadeToAsync(
                1,
                520,
                Easing.SinInOut);
        }
    }

    private void StopLoadingSkeletonPulse()
    {
        loadingSkeletonPulseCancellation?.Cancel();
        loadingSkeletonPulseCancellation?.Dispose();
        loadingSkeletonPulseCancellation = null;
        DataLoadingSkeletonPulseLayer.CancelAnimations();
        DataLoadingSkeletonPulseLayer.Opacity = 1;
    }

    private async Task HideLoadingSkeletonAsync()
    {
        isDataLoadingSkeletonShown = false;
        StopLoadingSkeletonPulse();
        DataLoadingOverlay.CancelAnimations();

        if (DataLoadingOverlay.IsVisible)
        {
            await DataLoadingOverlay.FadeToAsync(0, 100, Easing.CubicIn);
        }

        DataLoadingOverlay.IsVisible = false;
        DataLoadingOverlay.Opacity = 0;
    }
}
