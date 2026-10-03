using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class ExpensesView
{
    private async Task FocusStackedTransactionAsync(
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

        BoxView? highlight = null;
        try
        {
            await LoadDisplayedPeriodAsync(cancelPendingFocus: false);
            cancellationToken.ThrowIfCancellationRequested();

            for (var attempt = 0; attempt < 20 && highlight is null; attempt++)
            {
                await Task.Delay(50, cancellationToken);
                highlight = ActivityGroupsLayout
                    .GetVisualTreeDescendants()
                    .OfType<BoxView>()
                    .FirstOrDefault(view =>
                        view.ClassId == "TransactionSearchHighlight" &&
                        view.BindingContext is TransactionActivityItem item &&
                        item.Id == transactionId);
            }

            if (highlight is null)
            {
                if (revealTargetAsync is not null)
                {
                    await revealTargetAsync();
                }

                return;
            }

            await Task.Delay(80, cancellationToken);
            if (revealTargetAsync is not null)
            {
                await revealTargetAsync();
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            var targetOffset = GetVerticalOffsetWithinScrollContent(highlight);
            if (targetOffset >= 0)
            {
                var visibleTop = TransactionsScrollView.ScrollY;
                var visibleBottom =
                    visibleTop + TransactionsScrollView.Height - 90;
                var targetBottom = targetOffset + highlight.Height;
                var isAlreadyVisible =
                    targetOffset >= visibleTop &&
                    targetBottom <= visibleBottom;
                if (!isAlreadyVisible)
                {
                    var centeredOffset = Math.Max(
                        0,
                        targetOffset -
                        ((TransactionsScrollView.Height - highlight.Height) / 2));
                    var scrollTask = TransactionsScrollView.ScrollToAsync(
                        0,
                        centeredOffset,
                        animated: true);
                    await Task.WhenAny(
                        scrollTask,
                        Task.Delay(650, cancellationToken));
                }
            }
            else
            {
                var scrollTask = TransactionsScrollView.ScrollToAsync(
                    highlight,
                    ScrollToPosition.Center,
                    animated: true);
                await Task.WhenAny(
                    scrollTask,
                    Task.Delay(650, cancellationToken));
            }

            cancellationToken.ThrowIfCancellationRequested();
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
            if (highlight is not null)
            {
                highlight.CancelAnimations();
                highlight.Opacity = 0;
            }

            ReleaseTransactionFocusCancellation(focusCancellation);
        }
    }

    private void OnTransactionsScrollViewScrolled(
        object? sender,
        ScrolledEventArgs e) =>
        UpdateStackedStickyActivityHeader(e.ScrollY);

    private void OnActivityGroupsLayoutSizeChanged(
        object? sender,
        EventArgs e) =>
        UpdateStackedStickyActivityHeader(TransactionsScrollView.ScrollY);

    private void UpdateStackedStickyActivityHeader(double scrollY)
    {
        if (!ActivityGroupsLayout.IsVisible)
        {
            HideStickyActivityHeader();
            return;
        }

        var activationOffset = scrollY + StickyActivityHeader.Margin.Top;
        var firstGroupOffset = double.NaN;
        var nextGroupOffset = double.NaN;
        TransactionActivityGroup? activeGroup = null;

        foreach (var child in ActivityGroupsLayout.Children)
        {
            if (child is not VisualElement groupView ||
                groupView.BindingContext is not TransactionActivityGroup group)
            {
                continue;
            }

            var offset = GetVerticalOffsetWithinScrollContent(groupView);
            if (double.IsNaN(firstGroupOffset))
            {
                firstGroupOffset = offset;
            }

            if (offset >= 0 && offset <= activationOffset)
            {
                activeGroup = group;
                continue;
            }

            if (activeGroup is not null && offset >= 0)
            {
                nextGroupOffset = offset;
                break;
            }
        }

        if (double.IsNaN(firstGroupOffset) ||
            firstGroupOffset <= 1 ||
            activeGroup is null)
        {
            HideStickyActivityHeader();
            return;
        }

        if (!ReferenceEquals(stickyActivityGroup, activeGroup))
        {
            stickyActivityGroup = activeGroup;
            StickyActivityHeader.BindingContext = activeGroup;
        }

        StickyActivityHeader.IsVisible = true;
        var stickyHeight = Math.Max(StickyActivityHeader.Height, 42);
        var translationY = 0d;
        if (!double.IsNaN(nextGroupOffset))
        {
            var nextHeaderTop = nextGroupOffset - activationOffset;
            if (nextHeaderTop < stickyHeight)
            {
                translationY = Math.Min(0, nextHeaderTop - stickyHeight);
            }
        }

        StickyActivityHeader.TranslationY = translationY;
    }

    private double GetVerticalOffsetWithinScrollContent(VisualElement target)
    {
        var scrollContent = TransactionsScrollView.Content;
        if (scrollContent is null)
        {
            return -1;
        }

        var offset = 0d;
        Element? current = target;
        while (current is VisualElement visual &&
               !ReferenceEquals(current, scrollContent))
        {
            offset += visual.Y;
            current = visual.Parent;
        }

        return ReferenceEquals(current, scrollContent) ? offset : -1;
    }
}
