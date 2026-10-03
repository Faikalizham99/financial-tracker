using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class ExpensesView
{
    private const double ActivityGroupSpacing = 19;
    private const int SearchScrollRetryInterval = 8;
    private const int SearchScrollMaximumAttempts = 80;
    private static readonly TimeSpan SearchScrollPollInterval =
        TimeSpan.FromMilliseconds(50);
    private readonly Dictionary<DateTime, double> activityGroupHeights = [];
    private IReadOnlyList<TransactionActivityGroup> activityGroups = [];
    private int firstVisibleActivityGroupIndex = -1;
    private int lastVisibleActivityGroupIndex = -1;
    private double transactionHeaderHeight;
    private double transactionsVerticalOffset;

    private void SetActivityGroups(
        IReadOnlyList<TransactionActivityGroup> groups)
    {
        activityGroups = groups;
        activityGroupHeights.Clear();
        firstVisibleActivityGroupIndex = -1;
        lastVisibleActivityGroupIndex = -1;
        transactionsVerticalOffset = 0;
        TransactionsCollectionView.ItemsSource = groups;
    }

    private async Task FocusVirtualizedTransactionAsync(
        int transactionId,
        DateTime transactionDate,
        Func<Task>? revealTargetAsync)
    {
        CancelTransactionFocusAnimation();
        var focusCancellation = new CancellationTokenSource();
        transactionFocusCancellation = focusCancellation;
        var cancellationToken = focusCancellation.Token;
        displayedMonth = new DateTime(
            transactionDate.Year,
            transactionDate.Month,
            1);
        selectedPaymentFilter = null;
        selectedCategoryFilter = null;
        selectedStartDate = null;
        selectedEndDate = null;
        collapsedActivityGroupDates.Remove(transactionDate.Date);

        try
        {
            await LoadDisplayedPeriodAsync(cancelPendingFocus: false);
            cancellationToken.ThrowIfCancellationRequested();

            var targetGroup = activityGroups.FirstOrDefault(group =>
                group.Items.Any(item => item.Id == transactionId));
            if (targetGroup is null)
            {
                if (revealTargetAsync is not null)
                {
                    await revealTargetAsync();
                }
                return;
            }

            if (revealTargetAsync is not null)
            {
                await revealTargetAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }

            await WaitForTransactionCollectionLayoutAsync(cancellationToken);
            TransactionsCollectionView.ScrollTo(
                targetGroup,
                position: ScrollToPosition.Center,
                animate: true);

            var targetGroupIndex = -1;
            for (var index = 0; index < activityGroups.Count; index++)
            {
                if (!ReferenceEquals(activityGroups[index], targetGroup))
                {
                    continue;
                }

                targetGroupIndex = index;
                break;
            }
            var highlight = await WaitForRealizedHighlightAsync(
                transactionId,
                targetGroup,
                targetGroupIndex,
                cancellationToken);
            if (highlight is null)
            {
                return;
            }

            highlight.CancelAnimations();
            highlight.Opacity = 0;
            for (var pulse = 0; pulse < 3; pulse++)
            {
                await highlight.FadeToAsync(0.9, 250, Easing.CubicOut);
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(250, cancellationToken);
                await highlight.FadeToAsync(0, 250, Easing.CubicIn);
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(250, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            ResetRealizedSearchHighlights();
            ReleaseTransactionFocusCancellation(focusCancellation);
        }
    }

    private async Task<BoxView?> WaitForRealizedHighlightAsync(
        int transactionId,
        TransactionActivityGroup targetGroup,
        int targetGroupIndex,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < SearchScrollMaximumAttempts; attempt++)
        {
            var highlight = FindVisibleTransactionHighlight(
                transactionId,
                targetGroup,
                targetGroupIndex);
            if (highlight is not null)
            {
                return highlight;
            }

            if (attempt > 0 &&
                attempt % SearchScrollRetryInterval == 0)
            {
                // A long animated jump can take longer than the original
                // realization window. While virtualized item heights are being
                // learned, a handler can also stop an early request short of
                // the target. Reissuing the exact destination converges on the
                // requested row without relying on an estimated pixel offset.
                TransactionsCollectionView.ScrollTo(
                    targetGroup,
                    position: ScrollToPosition.Center,
                    animate: false);
            }

            await Task.Delay(SearchScrollPollInterval, cancellationToken);
        }

        return null;
    }

    private async Task WaitForTransactionCollectionLayoutAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TransactionsCollectionView.IsLoaded &&
                TransactionsCollectionView.Width > 0 &&
                TransactionsCollectionView.Height > 0)
            {
                return;
            }

            await Task.Delay(SearchScrollPollInterval, cancellationToken);
        }
    }

    private BoxView? FindVisibleTransactionHighlight(
        int transactionId,
        TransactionActivityGroup targetGroup,
        int targetGroupIndex)
    {
        var hasKnownVisibleRange =
            IsActivityGroupIndexValid(firstVisibleActivityGroupIndex) &&
            IsActivityGroupIndexValid(lastVisibleActivityGroupIndex);
        if (hasKnownVisibleRange &&
            (targetGroupIndex < firstVisibleActivityGroupIndex ||
             targetGroupIndex > lastVisibleActivityGroupIndex))
        {
            return null;
        }

        foreach (var groupView in FindRealizedActivityGroupViews(targetGroup))
        {
            if (!IsVisibleInTransactionViewport(groupView))
            {
                continue;
            }

            var highlight = groupView
                .GetVisualTreeDescendants()
                .OfType<BoxView>()
                .FirstOrDefault(view =>
                    view.ClassId == "TransactionSearchHighlight" &&
                    view.BindingContext is TransactionActivityItem item &&
                    item.Id == transactionId);
            if (highlight is not null &&
                IsVisibleInTransactionViewport(highlight))
            {
                return highlight;
            }
        }

        return null;
    }

    private bool IsVisibleInTransactionViewport(VisualElement view)
    {
        if (!view.IsVisible || view.Height <= 0 ||
            TransactionsCollectionView.Height <= 0)
        {
            return false;
        }

        var top = GetVerticalOffsetRelativeTo(
            view,
            TransactionsCollectionView);
        return !double.IsNaN(top) &&
               top < TransactionsCollectionView.Height &&
               top + view.Height > 0;
    }

    private VerticalStackLayout? FindRealizedActivityGroupView(
        TransactionActivityGroup group) =>
        FindRealizedActivityGroupViews(group).FirstOrDefault();

    private IEnumerable<VerticalStackLayout> FindRealizedActivityGroupViews(
        TransactionActivityGroup group) =>
        TransactionsCollectionView
            .GetVisualTreeDescendants()
            .OfType<VerticalStackLayout>()
            .Where(view =>
                view.ClassId == "TransactionActivityGroup" &&
                ReferenceEquals(view.BindingContext, group));

    private void OnTransactionsScrolled(
        object? sender,
        ItemsViewScrolledEventArgs e)
    {
        transactionsVerticalOffset = Math.Max(0, e.VerticalOffset);
        firstVisibleActivityGroupIndex = IsActivityGroupIndexValid(
            e.FirstVisibleItemIndex)
            ? e.FirstVisibleItemIndex
            : ResolveActivityGroupIndexFromScrollOffset(
                transactionsVerticalOffset);
        lastVisibleActivityGroupIndex = IsActivityGroupIndexValid(
            e.LastVisibleItemIndex)
            ? e.LastVisibleItemIndex
            : firstVisibleActivityGroupIndex;
        UpdateStickyActivityHeader();
    }

    private void OnTransactionHeaderContentSizeChanged(
        object? sender,
        EventArgs e)
    {
        transactionHeaderHeight = TransactionHeaderContent.Height;
        UpdateStickyActivityHeader();
    }

    private void OnActivityGroupItemSizeChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement
            {
                Height: > 0,
                BindingContext: TransactionActivityGroup group
            } view)
        {
            activityGroupHeights[group.Date] = view.Height;
        }

        UpdateStickyActivityHeader();
    }

    private void UpdateStickyActivityHeader()
    {
        if (!IsActivityGroupIndexValid(firstVisibleActivityGroupIndex))
        {
            firstVisibleActivityGroupIndex =
                ResolveActivityGroupIndexFromScrollOffset(
                    transactionsVerticalOffset);
        }

        if (activityGroups.Count == 0 ||
            transactionHeaderHeight <= 0 ||
            firstVisibleActivityGroupIndex < 0 ||
            transactionsVerticalOffset + StickyActivityHeader.Margin.Top <
                transactionHeaderHeight)
        {
            HideStickyActivityHeader();
            return;
        }

        var activeIndex = Math.Clamp(
            firstVisibleActivityGroupIndex,
            0,
            activityGroups.Count - 1);
        var activeGroup = activityGroups[activeIndex];
        if (!ReferenceEquals(stickyActivityGroup, activeGroup))
        {
            stickyActivityGroup = activeGroup;
            StickyActivityHeader.BindingContext = activeGroup;
        }

        StickyActivityHeader.IsVisible = true;
        StickyActivityHeader.TranslationY = CalculateStickyHeaderTranslation(
            activeIndex);
    }

    private bool IsActivityGroupIndexValid(int index) =>
        index >= 0 && index < activityGroups.Count;

    private int ResolveActivityGroupIndexFromScrollOffset(double scrollOffset)
    {
        if (activityGroups.Count == 0 || transactionHeaderHeight <= 0)
        {
            return -1;
        }

        var activationOffset =
            scrollOffset + StickyActivityHeader.Margin.Top;
        if (activationOffset < transactionHeaderHeight)
        {
            return -1;
        }

        var nextGroupTop = transactionHeaderHeight;
        for (var index = 0; index < activityGroups.Count; index++)
        {
            nextGroupTop += GetActivityGroupHeight(activityGroups[index]);
            nextGroupTop += ActivityGroupSpacing;
            if (activationOffset < nextGroupTop)
            {
                return index;
            }
        }

        // The collection footer can be the only reported visible element near
        // the end. The last date remains the active group throughout it.
        return activityGroups.Count - 1;
    }

    private double CalculateStickyHeaderTranslation(int activeIndex)
    {
        if (activeIndex >= activityGroups.Count - 1)
        {
            return 0;
        }

        var nextGroupView = FindRealizedActivityGroupView(
            activityGroups[activeIndex + 1]);
        if (nextGroupView is null)
        {
            return 0;
        }

        var nextHeaderTop = GetVerticalOffsetRelativeTo(
            nextGroupView,
            this);
        if (double.IsNaN(nextHeaderTop))
        {
            return 0;
        }

        var stickyHeight = Math.Max(StickyActivityHeader.Height, 42);
        var overlap = nextHeaderTop - StickyActivityHeader.Margin.Top - stickyHeight;
        return Math.Min(0, overlap);
    }

    private static double GetVerticalOffsetRelativeTo(
        VisualElement target,
        VisualElement ancestor)
    {
        var offset = 0d;
        Element? current = target;
        while (current is VisualElement visual &&
               !ReferenceEquals(current, ancestor))
        {
            offset += visual.Y;
            current = visual.Parent;
        }

        return ReferenceEquals(current, ancestor)
            ? offset
            : double.NaN;
    }

    private double GetActivityGroupHeight(TransactionActivityGroup group)
    {
        if (activityGroupHeights.TryGetValue(group.Date, out var height))
        {
            return height;
        }

        if (!group.IsExpanded)
        {
            return 28;
        }

        return 45 + (group.Items.Count * 63);
    }

    private void HideStickyActivityHeader()
    {
        if (!StickyActivityHeader.IsVisible && stickyActivityGroup is null)
        {
            return;
        }

        StickyActivityHeader.IsVisible = false;
        StickyActivityHeader.TranslationY = 0;
        StickyActivityHeader.BindingContext = null;
        stickyActivityGroup = null;
    }

    private void CancelTransactionFocusAnimation()
    {
        var cancellation = transactionFocusCancellation;
        transactionFocusCancellation = null;
        cancellation?.Cancel();
        ResetRealizedSearchHighlights();
    }

    private void ResetRealizedSearchHighlights()
    {
        foreach (var highlight in TransactionsCollectionView
                     .GetVisualTreeDescendants()
                     .OfType<BoxView>()
                     .Where(view =>
                         view.ClassId == "TransactionSearchHighlight"))
        {
            highlight.CancelAnimations();
            highlight.Opacity = 0;
        }
    }

    private void ReleaseTransactionFocusCancellation(
        CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(transactionFocusCancellation, cancellation))
        {
            transactionFocusCancellation = null;
        }

        cancellation.Dispose();
    }
}
