using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.ViewModels;

namespace FinancialTracker.Views;

public partial class BudgetSettingsView : ContentView
{
    private bool isOpen;
    private bool isAnimating;
    private bool isOperationRunning;
    private bool isHistorySelected;

    public event Action<bool>? VisibilityChanged;

    public bool IsOpen => isOpen;

    private BudgetSettingsViewModel? ViewModel => BindingContext as BudgetSettingsViewModel;

    public BudgetSettingsView()
    {
        InitializeComponent();
    }

    public async Task OpenAsync(DateTime month)
    {
        if (isOpen || isAnimating || ViewModel is null)
        {
            return;
        }

        isOpen = true;
        isAnimating = true;
        ViewModel.HistoryMonths.Clear();
        SetSelectedTab(historySelected: false);
        VisibilityChanged?.Invoke(true);
        IsVisible = true;
        Opacity = 0;
        PageContent.TranslationX = 36;
        QueueSettingScrollReset();

        var loadTask = ViewModel.OpenAsync(month);
        try
        {
            await Task.WhenAll(
                this.FadeToAsync(1, 170, Easing.CubicOut),
                PageContent.TranslateToAsync(0, 0, 240, Easing.CubicOut),
                loadTask);
        }
        catch
        {
            IsVisible = false;
            Opacity = 1;
            PageContent.TranslationX = 0;
            isOpen = false;
            VisibilityChanged?.Invoke(false);
            throw;
        }
        finally
        {
            isAnimating = false;
        }
    }

    public async Task CloseAsync()
    {
        if (!isOpen || isAnimating)
        {
            return;
        }

        isAnimating = true;
        IncludingInvestmentEntry.Unfocus();
        ExcludingInvestmentEntry.Unfocus();
        try
        {
            await Task.WhenAll(
                this.FadeToAsync(0, 155, Easing.CubicIn),
                PageContent.TranslateToAsync(32, 0, 190, Easing.CubicIn));
        }
        finally
        {
            IsVisible = false;
            Opacity = 1;
            PageContent.TranslationX = 0;
            isOpen = false;
            isAnimating = false;
            VisibilityChanged?.Invoke(false);
        }
    }

    public async Task HandleBackAsync()
    {
        if (MonthPicker.IsOpen)
        {
            await MonthPicker.DismissAsync();
            return;
        }

        await CloseAsync();
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseAsync();
        await feedback;
    }

    private async void OnTabTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string tab || ViewModel is null || isOperationRunning)
        {
            return;
        }

        var selectHistory = tab == "History";
        if (selectHistory == isHistorySelected)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        SetSelectedTab(selectHistory);
        if (selectHistory &&
            (ViewModel.HistoryMonths.Count == 0 ||
             ViewModel.HistoryYear != ViewModel.SelectedMonth.Year))
        {
            await RunOperationAsync(() => ViewModel.LoadHistoryYearAsync(ViewModel.SelectedMonth.Year));
            HistoryCollection.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
        }

        await feedback;
    }

    private void SetSelectedTab(bool historySelected)
    {
        isHistorySelected = historySelected;
        SettingScrollView.IsVisible = !historySelected;
        HistoryPanel.IsVisible = historySelected;
        SettingTab.Style = GetStyle(historySelected ? "CategorySegmentTab" : "SelectedCategorySegmentTab");
        SettingTabLabel.Style = GetStyle(historySelected ? "CategorySegmentLabel" : "SelectedCategorySegmentLabel");
        HistoryTab.Style = GetStyle(historySelected ? "SelectedCategorySegmentTab" : "CategorySegmentTab");
        HistoryTabLabel.Style = GetStyle(historySelected ? "SelectedCategorySegmentLabel" : "CategorySegmentLabel");
    }

    private async void OnPreviousMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeMonthAsync(-1, sender);

    private async void OnNextMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeMonthAsync(1, sender);

    private async Task ChangeMonthAsync(int offset, object? sender)
    {
        if (ViewModel is null)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        await RunOperationAsync(() => ViewModel.ChangeMonthAsync(offset));
        await feedback;
    }

    private async void OnChooseMonthTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is null || isOperationRunning)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        IncludingInvestmentEntry.Unfocus();
        ExcludingInvestmentEntry.Unfocus();
        var selected = await MonthPicker.PickAsync(
            ViewModel.SelectedMonth,
            BudgetSettingsViewModel.MinimumBudgetYear,
            BudgetSettingsViewModel.MaximumBudgetYear);
        if (selected.HasValue)
        {
            await RunOperationAsync(() => ViewModel.SelectMonthAsync(selected.Value));
        }

        await feedback;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (ViewModel is null || !ViewModel.CanSave)
        {
            return;
        }

        IncludingInvestmentEntry.Unfocus();
        ExcludingInvestmentEntry.Unfocus();
        await RunOperationAsync(ViewModel.SaveAsync);
    }

    private async void OnPreviousHistoryYearTapped(object? sender, TappedEventArgs e) =>
        await ChangeHistoryYearAsync(-1, sender);

    private async void OnNextHistoryYearTapped(object? sender, TappedEventArgs e) =>
        await ChangeHistoryYearAsync(1, sender);

    private async Task ChangeHistoryYearAsync(int offset, object? sender)
    {
        if (ViewModel is null)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        await RunOperationAsync(() => ViewModel.ChangeHistoryYearAsync(offset));
        HistoryCollection.ScrollTo(0, position: ScrollToPosition.Start, animate: false);
        await feedback;
    }

    private async void OnHistoryMonthSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null || e.CurrentSelection.FirstOrDefault() is not BudgetHistoryMonthItem item)
        {
            return;
        }

        HistoryCollection.SelectedItem = null;
        await RunOperationAsync(() => ViewModel.SelectMonthAsync(item.Month));
        SetSelectedTab(historySelected: false);
    }

    private void OnHistoryCollectionSizeChanged(object? sender, EventArgs e)
    {
        if (HistoryCollection.ItemsLayout is GridItemsLayout layout)
        {
            layout.Span = HistoryCollection.Width switch
            {
                >= 1050 => 4,
                >= 720 => 3,
                _ => 2
            };
        }
    }

    private async Task RunOperationAsync(Func<Task> operation)
    {
        if (isOperationRunning)
        {
            return;
        }

        isOperationRunning = true;
        try
        {
            await operation();
        }
        finally
        {
            isOperationRunning = false;
        }
    }

    private void QueueSettingScrollReset()
    {
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                await SettingScrollView.ScrollToAsync(0, 0, false);
            }
            catch (ObjectDisposedException)
            {
            }
        });
    }

    private static Style GetStyle(string key) =>
        (Style)Application.Current!.Resources[key];
}
