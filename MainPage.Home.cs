using FinancialTracker.Controls;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using Microsoft.Maui.Storage;

namespace FinancialTracker;

public partial class MainPage
{
    private async void OnDashboardTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(0);

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await NavigateToSectionAsync(3);
        await feedback;
    }

    private async void OnDashboardViewAllTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await NavigateToSectionAsync(1);
        await feedback;
    }

    private async void OnArrangeHomeTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await OpenArrangeHomeAsync();
        await feedback;
    }

    private async Task OpenArrangeHomeAsync()
    {
        if (isArrangeHomeOpen || isArrangeHomeAnimating)
        {
            return;
        }

        draftHomeSectionOrder.Clear();
        draftHomeSectionOrder.AddRange(homeSectionOrder);
        RefreshArrangeHomeSections();
        isArrangeHomeOpen = true;
        isArrangeHomeAnimating = true;
        ArrangeHomeOverlay.IsVisible = true;
        ArrangeHomeOverlay.Opacity = 0;
        ArrangeHomeCard.Opacity = 0;
        ArrangeHomeCard.TranslationY = 24;

        try
        {
            await Task.WhenAll(
                ArrangeHomeOverlay.FadeToAsync(1, 150, Easing.CubicOut),
                ArrangeHomeCard.FadeToAsync(1, 180, Easing.CubicOut),
                ArrangeHomeCard.TranslateToAsync(0, 0, 210, Easing.CubicOut));
        }
        finally
        {
            isArrangeHomeAnimating = false;
        }
    }

    private async void OnHomeSectionDragUpdated(
        object? sender,
        VerticalDragUpdatedEventArgs e)
    {
        if (sender is not Grid row ||
            row.BindingContext is not HomeSectionOption section)
        {
            return;
        }

        switch (e.Status)
        {
            case VerticalDragStatus.Started:
                StartHomeSectionDrag(section.Key, row);
                break;
            case VerticalDragStatus.Running:
                UpdateHomeSectionDrag(row, e.TotalY);
                break;
            case VerticalDragStatus.Completed:
                UpdateHomeSectionDrag(row, e.TotalY);
                await CompleteHomeSectionDragAsync(row, shouldReorder: true);
                break;
            case VerticalDragStatus.Canceled:
                await CompleteHomeSectionDragAsync(row, shouldReorder: false);
                break;
        }
    }

    private void OnHomeSectionPointerPressed(object? sender, PointerEventArgs e)
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI || isHomeSectionReordering)
        {
            return;
        }

        var position = e.GetPosition(ArrangeHomeSectionsLayout);
        if (position is null)
        {
            return;
        }

        var row = ArrangeHomeSectionsLayout.Children
            .OfType<Grid>()
            .FirstOrDefault(candidate => candidate.Bounds.Contains(position.Value));
        if (row?.BindingContext is not HomeSectionOption section)
        {
            return;
        }

        StartHomeSectionDrag(section.Key, row);
        if (ReferenceEquals(row, draggedHomeSectionRow))
        {
            homeSectionPointerStart = position;
        }
    }

    private void OnHomeSectionPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI ||
            draggedHomeSectionRow is null ||
            homeSectionPointerStart is null)
        {
            return;
        }

        var position = e.GetPosition(ArrangeHomeSectionsLayout);
        if (position is not null)
        {
            UpdateHomeSectionDrag(
                draggedHomeSectionRow,
                position.Value.Y - homeSectionPointerStart.Value.Y);
        }
    }

    private async void OnHomeSectionPointerReleased(object? sender, PointerEventArgs e)
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI ||
            draggedHomeSectionRow is not Grid row)
        {
            return;
        }

        OnHomeSectionPointerMoved(sender, e);
        homeSectionPointerStart = null;
        await CompleteHomeSectionDragAsync(row, shouldReorder: true);
    }

    private async void OnHomeSectionPointerExited(object? sender, PointerEventArgs e)
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI ||
            draggedHomeSectionRow is not Grid row)
        {
            return;
        }

        homeSectionPointerStart = null;
        await CompleteHomeSectionDragAsync(row, shouldReorder: false);
    }

    private void StartHomeSectionDrag(string key, Grid row)
    {
        if (isHomeSectionReordering)
        {
            return;
        }

        var startIndex = draftHomeSectionOrder.IndexOf(key);
        if (startIndex < 0)
        {
            return;
        }

        isHomeSectionReordering = true;
        draggedHomeSectionRow = row;
        draggedHomeSectionKey = key;
        draggedHomeSectionStartIndex = startIndex;
        draggedHomeSectionTargetIndex = startIndex;
        draggedHomeSectionOffset = 0;
        row.ZIndex = 10;
        row.Opacity = 0.94;
        _ = row.ScaleToAsync(1.02, 90, Easing.CubicOut);
    }

    private void UpdateHomeSectionDrag(Grid row, double totalY)
    {
        if (!ReferenceEquals(row, draggedHomeSectionRow))
        {
            return;
        }

        var rowHeight = Math.Max(row.Height, 53);
        var minimumOffset = -draggedHomeSectionStartIndex * rowHeight;
        var maximumOffset = (draftHomeSectionOrder.Count - 1 - draggedHomeSectionStartIndex) * rowHeight;
        draggedHomeSectionOffset = Math.Clamp(totalY, minimumOffset, maximumOffset);
        row.TranslationY = draggedHomeSectionOffset;

        var indexOffset = (int)Math.Round(
            draggedHomeSectionOffset / rowHeight,
            MidpointRounding.AwayFromZero);
        draggedHomeSectionTargetIndex = Math.Clamp(
            draggedHomeSectionStartIndex + indexOffset,
            0,
            draftHomeSectionOrder.Count - 1);
        UpdateHomeSectionInsertionGap(row, rowHeight);
    }

    private void UpdateHomeSectionInsertionGap(Grid draggedRow, double rowHeight)
    {
        var rows = ArrangeHomeSectionsLayout.Children.OfType<Grid>().ToList();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (ReferenceEquals(row, draggedRow))
            {
                continue;
            }

            row.TranslationY = draggedHomeSectionTargetIndex switch
            {
                _ when draggedHomeSectionTargetIndex > draggedHomeSectionStartIndex &&
                    index > draggedHomeSectionStartIndex &&
                    index <= draggedHomeSectionTargetIndex => -rowHeight,
                _ when draggedHomeSectionTargetIndex < draggedHomeSectionStartIndex &&
                    index >= draggedHomeSectionTargetIndex &&
                    index < draggedHomeSectionStartIndex => rowHeight,
                _ => 0
            };
        }
    }

    private async Task RestoreHomeSectionRowsAsync(Grid draggedRow)
    {
        var animations = ArrangeHomeSectionsLayout.Children
            .OfType<Grid>()
            .Select(row => row.TranslateToAsync(0, 0, 130, Easing.CubicOut))
            .Cast<Task>()
            .ToList();
        animations.Add(draggedRow.ScaleToAsync(1, 130, Easing.CubicOut));
        await Task.WhenAll(animations);
    }

    private async Task CompleteHomeSectionDragAsync(Grid row, bool shouldReorder)
    {
        if (!ReferenceEquals(row, draggedHomeSectionRow) ||
            draggedHomeSectionKey is null ||
            draggedHomeSectionStartIndex < 0)
        {
            return;
        }

        var key = draggedHomeSectionKey;
        var startIndex = draggedHomeSectionStartIndex;
        var rowHeight = Math.Max(row.Height, 53);
        var targetIndex = shouldReorder
            ? draggedHomeSectionTargetIndex
            : startIndex;

        try
        {
            if (targetIndex == startIndex)
            {
                await RestoreHomeSectionRowsAsync(row);
                return;
            }

            await row.TranslateToAsync(0, (targetIndex - startIndex) * rowHeight, 90, Easing.CubicOut);
            await ArrangeHomeSectionsLayout.FadeToAsync(0.42, 65, Easing.CubicIn);
            draftHomeSectionOrder.RemoveAt(startIndex);
            draftHomeSectionOrder.Insert(targetIndex, key);
            ArrangeHomeSectionsLayout.TranslationY = targetIndex < startIndex ? -7 : 7;
            RefreshArrangeHomeSections();
            await Task.WhenAll(
                ArrangeHomeSectionsLayout.FadeToAsync(1, 160, Easing.CubicOut),
                ArrangeHomeSectionsLayout.TranslateToAsync(0, 0, 180, Easing.CubicOut));
        }
        finally
        {
            foreach (var sectionRow in ArrangeHomeSectionsLayout.Children.OfType<Grid>())
            {
                sectionRow.TranslationY = 0;
            }

            row.TranslationY = 0;
            row.Scale = 1;
            row.Opacity = 1;
            row.ZIndex = 0;
            draggedHomeSectionRow = null;
            draggedHomeSectionKey = null;
            draggedHomeSectionStartIndex = -1;
            draggedHomeSectionTargetIndex = -1;
            draggedHomeSectionOffset = 0;
            homeSectionPointerStart = null;
            isHomeSectionReordering = false;
        }
    }

    private async void OnArrangeHomeResetTapped(object? sender, TappedEventArgs e)
    {
        if (isHomeSectionReordering)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        isHomeSectionReordering = true;
        try
        {
            await ArrangeHomeSectionsLayout.FadeToAsync(0.45, 80, Easing.CubicIn);
            draftHomeSectionOrder.Clear();
            draftHomeSectionOrder.AddRange(DefaultHomeSectionOrder);
            ArrangeHomeSectionsLayout.TranslationY = -7;
            RefreshArrangeHomeSections();
            await Task.WhenAll(
                ArrangeHomeSectionsLayout.FadeToAsync(1, 160, Easing.CubicOut),
                ArrangeHomeSectionsLayout.TranslateToAsync(0, 0, 180, Easing.CubicOut),
                feedback);
        }
        finally
        {
            ArrangeHomeSectionsLayout.Opacity = 1;
            ArrangeHomeSectionsLayout.TranslationY = 0;
            isHomeSectionReordering = false;
        }
    }

    private async void OnArrangeHomeSaveTapped(object? sender, TappedEventArgs e)
    {
        if (isHomeSectionReordering)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        homeSectionOrder.Clear();
        homeSectionOrder.AddRange(draftHomeSectionOrder);
        Preferences.Default.Set(
            HomeSectionOrderPreferenceKey,
            string.Join('|', homeSectionOrder));
        await CloseArrangeHomeAsync();
        await ApplyHomeSectionOrderAsync();
        await feedback;
    }

    private async void OnArrangeHomeBackdropTapped(object? sender, TappedEventArgs e) =>
        await CloseArrangeHomeAsync();

    private async void OnArrangeHomeCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseArrangeHomeAsync();
        await feedback;
    }

    private async Task CloseArrangeHomeAsync()
    {
        if (!isArrangeHomeOpen || isArrangeHomeAnimating || isHomeSectionReordering)
        {
            return;
        }

        isArrangeHomeAnimating = true;
        try
        {
            await Task.WhenAll(
                ArrangeHomeOverlay.FadeToAsync(0, 130, Easing.CubicIn),
                ArrangeHomeCard.TranslateToAsync(0, 20, 150, Easing.CubicIn));
        }
        finally
        {
            ArrangeHomeOverlay.IsVisible = false;
            ArrangeHomeOverlay.Opacity = 0;
            ArrangeHomeCard.Opacity = 1;
            ArrangeHomeCard.TranslationY = 0;
            isArrangeHomeOpen = false;
            isArrangeHomeAnimating = false;
        }
    }

    private void RefreshArrangeHomeSections()
    {
        var sections = draftHomeSectionOrder
            .Select((key, index) => new HomeSectionOption(
                key,
                HomeSectionTitles[key],
                index < draftHomeSectionOrder.Count - 1))
            .ToList();
        BindableLayout.SetItemsSource(ArrangeHomeSectionsLayout, sections);
    }

    private void LoadHomeSectionOrder()
    {
        var savedOrder = Preferences.Default
            .Get(HomeSectionOrderPreferenceKey, string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        homeSectionOrder.Clear();
        homeSectionOrder.AddRange(savedOrder
            .Where(HomeSectionTitles.ContainsKey)
            .Distinct(StringComparer.Ordinal));
        homeSectionOrder.AddRange(DefaultHomeSectionOrder
            .Where(key => !homeSectionOrder.Contains(key, StringComparer.Ordinal)));
    }

    private void ApplyHomeSectionOrder()
    {
        foreach (var key in homeSectionOrder)
        {
            var section = GetHomeSection(key);
            DashboardReorderableSections.Children.Remove(section);
            DashboardReorderableSections.Children.Add(section);
        }
    }

    private async Task ApplyHomeSectionOrderAsync()
    {
        await Task.WhenAll(
            DashboardReorderableSections.FadeToAsync(0.35, 100, Easing.CubicIn),
            DashboardReorderableSections.TranslateToAsync(0, 10, 110, Easing.CubicIn));
        ApplyHomeSectionOrder();
        DashboardReorderableSections.TranslationY = -10;
        await Task.WhenAll(
            DashboardReorderableSections.FadeToAsync(1, 220, Easing.CubicOut),
            DashboardReorderableSections.TranslateToAsync(0, 0, 230, Easing.CubicOut));
    }

    private View GetHomeSection(string key) =>
        key switch
        {
            "glance" => DashboardGlanceSection,
            "insight" => DashboardInsightSection,
            "expense_categories" => DashboardExpenseCategoriesSection,
            "income_categories" => DashboardIncomeCategoriesSection,
            "recent_activity" => DashboardRecentActivitySection,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null)
        };

    private void UpdateDashboardGreeting()
    {
        DashboardGreetingLabel.Text = DateTime.Now.Hour switch
        {
            >= 5 and < 12 => "Good morning,",
            >= 12 and < 17 => "Good afternoon,",
            _ => "Good evening,"
        };
    }

    private void ApplyTransactionData(
        TransactionDataSnapshot snapshot,
        CurrencyOption currency)
    {
        ExpensesView.Refresh(snapshot.PeriodRecords, currency);
        TransactionSearchView.SetCurrency(currency);
        RefreshDashboard(snapshot.DashboardRecords, currency);
    }

    private void RefreshDashboard(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency)
    {
        DashboardMonthlySummary.Refresh(records, selectedCurrency);
        var summary = DashboardSummaryBuilder.Build(
            records,
            selectedCurrency,
            canModifyTransactions: !isTransactionEditingLocked,
            includeInvestment: includeInvestmentInTotals,
            today: DateTime.Today);

        TodaySpentValueLabel.Text = summary.TodaySpentText;
        DailyAverageValueLabel.Text = summary.DailyAverageText;
        DaysRemainingValueLabel.Text = summary.DaysRemainingText;
        ExpenseCategoryTotalLabel.Text = summary.ExpenseTotalText;
        IncomeCategoryTotalLabel.Text = summary.IncomeTotalText;
        DashboardInsights.ItemsSource = summary.Insights;
        BindableLayout.SetItemsSource(
            DashboardExpenseCategoriesLayout,
            summary.ExpenseCategories);
        BindableLayout.SetItemsSource(
            DashboardIncomeCategoriesLayout,
            summary.IncomeCategories);
        dashboardRecentActivity = summary.RecentActivity;
        if (expandedDashboardTransactionDescriptionId is int expandedId)
        {
            var expandedItem = dashboardRecentActivity.FirstOrDefault(item => item.Id == expandedId);
            if (expandedItem is null)
            {
                expandedDashboardTransactionDescriptionId = null;
            }
            else
            {
                expandedItem.SetDescriptionExpanded(true);
            }
        }

        BindableLayout.SetItemsSource(
            DashboardActivityLayout,
            dashboardRecentActivity);
        var hasRecentActivity = dashboardRecentActivity.Count > 0;
        DashboardActivityCard.IsVisible = hasRecentActivity;
        DashboardEmptyActivityState.IsVisible = !hasRecentActivity;
        DashboardViewAllButton.IsVisible = hasRecentActivity;
        widgetSnapshotCoordinator.PublishIfChanged(
            records,
            selectedCurrency,
            includeInvestmentInTotals,
            DateTime.Today);
    }
}
