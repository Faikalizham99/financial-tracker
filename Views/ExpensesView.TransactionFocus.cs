using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class ExpensesView
{
    private const int SearchScrollMaximumAttempts = 100;
#if WINDOWS
    private const int SearchScrollMaximumCorrections = 8;
#else
    private const int SearchScrollMaximumCorrections = 1;
#endif
    private const int SearchScrollFirstCorrectionAttempt = 20;
    private const int SearchScrollCorrectionInterval = 8;
    private static readonly TimeSpan SearchScrollPollInterval =
        TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan SearchScrollSettleInterval =
        TimeSpan.FromMilliseconds(160);

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
            lastTransactionScrollAtUtc = DateTime.UtcNow;
            TransactionsCollectionView.ScrollTo(
                targetGroup,
                position: ScrollToPosition.Center,
                animate: false);

            var targetGroupIndex = FindActivityGroupIndex(targetGroup);
            var highlight = await WaitForVisibleHighlightAsync(
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

    private async Task<BoxView?> WaitForVisibleHighlightAsync(
        int transactionId,
        TransactionActivityGroup targetGroup,
        int targetGroupIndex,
        CancellationToken cancellationToken)
    {
        var correctionCount = 0;
        for (var attempt = 0; attempt < SearchScrollMaximumAttempts; attempt++)
        {
            var highlight = FindVisibleTransactionHighlight(
                transactionId,
                targetGroup,
                targetGroupIndex);
            if (highlight is not null && IsTransactionScrollSettled())
            {
                return highlight;
            }

            var shouldCorrect =
                highlight is null &&
                attempt >= SearchScrollFirstCorrectionAttempt &&
                (attempt - SearchScrollFirstCorrectionAttempt) %
                    SearchScrollCorrectionInterval == 0 &&
                correctionCount < SearchScrollMaximumCorrections;
            if (shouldCorrect)
            {
                correctionCount++;
                lastTransactionScrollAtUtc = DateTime.UtcNow;
                TransactionsCollectionView.ScrollTo(
                    targetGroup,
                    position: ScrollToPosition.Center,
                    animate: false);
            }

            await Task.Delay(SearchScrollPollInterval, cancellationToken);
        }

        return null;
    }

    private bool IsTransactionScrollSettled() =>
        DateTime.UtcNow - lastTransactionScrollAtUtc >=
        SearchScrollSettleInterval;

    private int FindActivityGroupIndex(TransactionActivityGroup targetGroup)
    {
        for (var index = 0; index < activityGroups.Count; index++)
        {
            if (ReferenceEquals(activityGroups[index], targetGroup))
            {
                return index;
            }
        }

        return -1;
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

        var groupView = FindRealizedActivityGroupView(targetGroup);
        if (groupView is null || !IsVisibleInTransactionViewport(groupView))
        {
            return null;
        }

        var highlight = groupView
            .GetVisualTreeDescendants()
            .OfType<BoxView>()
            .FirstOrDefault(view =>
                view.ClassId == "TransactionSearchHighlight" &&
                view.BindingContext is TransactionActivityItem item &&
                item.Id == transactionId);
        return highlight is not null &&
               IsVisibleInTransactionViewport(highlight)
            ? highlight
            : null;
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

    private void CancelTransactionFocusAnimation()
    {
        var cancellation = transactionFocusCancellation;
        transactionFocusCancellation = null;
        cancellation?.Cancel();
        ResetRealizedSearchHighlights();
    }

    private void ResetRealizedSearchHighlights()
    {
        foreach (var reference in realizedActivityGroupViews.Values)
        {
            if (!reference.TryGetTarget(out var groupView))
            {
                continue;
            }

            foreach (var highlight in groupView
                         .GetVisualTreeDescendants()
                         .OfType<BoxView>()
                         .Where(view =>
                             view.ClassId == "TransactionSearchHighlight"))
            {
                highlight.CancelAnimations();
                highlight.Opacity = 0;
            }
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
