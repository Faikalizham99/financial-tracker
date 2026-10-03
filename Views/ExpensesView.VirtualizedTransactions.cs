using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class ExpensesView
{
    private readonly Dictionary<DateTime, double> activityGroupHeights = [];
    private readonly Dictionary<
        TransactionActivityGroup,
        WeakReference<VerticalStackLayout>> realizedActivityGroupViews =
            new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<TransactionActivityGroup> activityGroups = [];
    private int firstVisibleActivityGroupIndex = -1;
    private int lastVisibleActivityGroupIndex = -1;
    private double transactionHeaderHeight;
    private double transactionsVerticalOffset;
    private DateTime lastTransactionScrollAtUtc = DateTime.MinValue;

    private void SetActivityGroups(
        IReadOnlyList<TransactionActivityGroup> groups)
    {
        activityGroups = groups;
        activityGroupHeights.Clear();
        realizedActivityGroupViews.Clear();
        firstVisibleActivityGroupIndex = -1;
        lastVisibleActivityGroupIndex = -1;
        transactionsVerticalOffset = 0;
        if (useVirtualizedTransactionList)
        {
            BindableLayout.SetItemsSource(ActivityGroupsLayout, null);
            ActivityGroupsLayout.IsVisible = false;
            TransactionsCollectionView.ItemsSource = groups;
            return;
        }

        TransactionsCollectionView.ItemsSource = null;
        BindableLayout.SetItemsSource(ActivityGroupsLayout, groups);
        ActivityGroupsLayout.IsVisible = groups.Count > 0;
    }

    private void OnTransactionsCollectionScrolled(
        object? sender,
        ItemsViewScrolledEventArgs e)
    {
        lastTransactionScrollAtUtc = DateTime.UtcNow;
        transactionsVerticalOffset = Math.Max(0, e.VerticalOffset);

        if (IsActivityGroupIndexValid(e.FirstVisibleItemIndex))
        {
            firstVisibleActivityGroupIndex = e.FirstVisibleItemIndex;
        }
        else
        {
            var resolvedIndex = ResolveActivityGroupIndexFromScrollOffset(
                transactionsVerticalOffset);
            if (IsActivityGroupIndexValid(resolvedIndex) ||
                transactionsVerticalOffset < transactionHeaderHeight)
            {
                firstVisibleActivityGroupIndex = resolvedIndex;
            }
        }

        if (IsActivityGroupIndexValid(e.LastVisibleItemIndex))
        {
            lastVisibleActivityGroupIndex = e.LastVisibleItemIndex;
        }
        else if (!IsActivityGroupIndexValid(lastVisibleActivityGroupIndex))
        {
            lastVisibleActivityGroupIndex = firstVisibleActivityGroupIndex;
        }

        UpdateStickyActivityHeader();
    }

    private void OnActivityGroupBindingContextChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is not VerticalStackLayout groupView)
        {
            return;
        }

        foreach (var entry in realizedActivityGroupViews.ToArray())
        {
            if (!entry.Value.TryGetTarget(out var cachedView) ||
                ReferenceEquals(cachedView, groupView))
            {
                realizedActivityGroupViews.Remove(entry.Key);
            }
        }

        if (groupView.BindingContext is TransactionActivityGroup group)
        {
            realizedActivityGroupViews[group] = new(groupView);
        }
    }

    private void OnActivityGroupItemSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not VerticalStackLayout
            {
                Height: > 0,
                BindingContext: TransactionActivityGroup group
            } groupView)
        {
            return;
        }

        realizedActivityGroupViews[group] = new(groupView);
        activityGroupHeights[group.Date] = groupView.Height;
    }

    private VerticalStackLayout? FindRealizedActivityGroupView(
        TransactionActivityGroup group)
    {
        if (!realizedActivityGroupViews.TryGetValue(group, out var reference) ||
            !reference.TryGetTarget(out var groupView) ||
            !ReferenceEquals(groupView.BindingContext, group))
        {
            realizedActivityGroupViews.Remove(group);
            return null;
        }

        return groupView;
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
}
