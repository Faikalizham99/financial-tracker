using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class ExpensesView
{
    private const double ActivityGroupSpacing = 19;

    private void OnTransactionHeaderContentSizeChanged(
        object? sender,
        EventArgs e)
    {
        transactionHeaderHeight = TransactionHeaderContent.Height;
        UpdateStickyActivityHeader();
    }

    private void UpdateStickyActivityHeader()
    {
        if (!useVirtualizedTransactionList)
        {
            UpdateStackedStickyActivityHeader(TransactionsScrollView.ScrollY);
            return;
        }

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
        if (ReferenceEquals(stickyActivityGroup, activeGroup) &&
            StickyActivityHeader.IsVisible)
        {
            return;
        }

        stickyActivityGroup = activeGroup;
        StickyActivityHeader.BindingContext = activeGroup;
        StickyActivityHeader.TranslationY = 0;
        StickyActivityHeader.IsVisible = true;
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

        return activityGroups.Count - 1;
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
}
