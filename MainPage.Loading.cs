using FinancialTracker.Helpers;
using FinancialTracker.Views.Skeletons;

namespace FinancialTracker;

public partial class MainPage
{
    private readonly Dictionary<int, View> dataLoadingSkeletons = [];

    private void UpdateLoadingSkeletonForSelectedSection()
    {
        if (!isDataLoadingSkeletonShown)
        {
            return;
        }

        SelectLoadingSkeleton(selectedSectionIndex);
        DataLoadingOverlay.IsVisible = true;
        DataLoadingOverlay.Opacity = 1;
    }

    private void ShowTransactionLoadingSkeleton()
    {
        isDataLoadingSkeletonShown = true;
        DataLoadingOverlay.CancelAnimations();
        SelectLoadingSkeleton(sectionIndex: 1);
        DataLoadingOverlay.Opacity = 1;
        DataLoadingOverlay.IsVisible = true;
    }

    private void ShowDataLoadingSkeletonForSelectedSection()
    {
        isDataLoadingSkeletonShown = true;
        DataLoadingOverlay.CancelAnimations();
        DataLoadingOverlay.ZIndex = 140;
        SelectLoadingSkeleton(selectedSectionIndex);
        DataLoadingOverlay.Opacity = 1;
        DataLoadingOverlay.IsVisible = true;
    }

    private void SelectLoadingSkeleton(int sectionIndex)
    {
        if (!dataLoadingSkeletons.TryGetValue(sectionIndex, out var skeleton))
        {
            skeleton = sectionIndex switch
            {
                0 => new HomeLoadingSkeletonView(),
                1 => new TransactionsLoadingSkeletonView(),
                2 => new AssetsLoadingSkeletonView(),
                3 => new SettingsLoadingSkeletonView(),
                _ => throw new ArgumentOutOfRangeException(nameof(sectionIndex))
            };
            dataLoadingSkeletons[sectionIndex] = skeleton;
        }

        if (!ReferenceEquals(DataLoadingOverlay.Content, skeleton))
        {
            DataLoadingOverlay.Content = skeleton;
        }
    }

    private async Task RunWithDataLoadingSkeletonAsync(Func<Task> operation)
    {
        await dataLoadingOperationLock.WaitAsync();
        try
        {
            await LoadingSkeletonDelayer.RunAsync(
                operation,
                async isVisible =>
                {
                    if (isVisible)
                    {
                        ShowDataLoadingSkeletonForSelectedSection();
                        return;
                    }

                    await HideLoadingSkeletonAsync();
                });
        }
        finally
        {
            DataLoadingOverlay.ZIndex = 20;
            dataLoadingOperationLock.Release();
        }
    }

    private async Task HideLoadingSkeletonAsync()
    {
        isDataLoadingSkeletonShown = false;
        DataLoadingOverlay.CancelAnimations();

        if (DataLoadingOverlay.IsVisible)
        {
            await DataLoadingOverlay.FadeToAsync(0, 100, Easing.CubicIn);
        }

        DataLoadingOverlay.IsVisible = false;
        DataLoadingOverlay.Opacity = 0;
    }
}
