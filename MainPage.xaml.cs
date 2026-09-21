namespace FinancialTracker;

using System.ComponentModel;
using System.Globalization;
using FinancialTracker.ViewModels;
using FinancialTracker.Controls;
using FinancialTracker.Data;
using FinancialTracker.Services;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

public partial class MainPage : ContentPage
{
    private const string HomeSectionOrderPreferenceKey = "home_section_order";
    private static readonly IReadOnlyList<string> DefaultHomeSectionOrder =
    [
        "glance",
        "insight",
        "expense_categories",
        "income_categories",
        "recent_activity"
    ];
    private static readonly IReadOnlyDictionary<string, string> HomeSectionTitles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["glance"] = "This month at a glance",
            ["insight"] = "Monthly insight",
            ["expense_categories"] = "Expense categories",
            ["income_categories"] = "Income categories",
            ["recent_activity"] = "Recent activity"
        };

    private readonly SettingsViewModel settingsViewModel;
    private readonly LocalDatabase localDatabase;
    private readonly SemaphoreSlim transactionRefreshLock = new(1, 1);
    private readonly SemaphoreSlim dataLoadingOperationLock = new(1, 1);
    private readonly List<string> homeSectionOrder = [];
    private readonly List<string> draftHomeSectionOrder = [];
    private readonly VisualElement[] navigationPages;
    private readonly VisualElement[] navigationIcons;
    private readonly Border[] navigationTabs;
    private readonly Label[] navigationLabels;
    private IReadOnlyList<TransactionRecord>? cachedTransactionRecords;
    private int selectedSectionIndex = 1;
    private int navigationTransitionVersion;
    private TransactionRecord? pendingDeleteTransaction;
    private bool isDeleteConfirmationAnimating;
    private bool isDeletingTransaction;
    private bool isArrangeHomeOpen;
    private bool isArrangeHomeAnimating;
    private bool isHomeSectionReordering;
    private Grid? draggedHomeSectionRow;
    private string? draggedHomeSectionKey;
    private int draggedHomeSectionStartIndex = -1;
    private double draggedHomeSectionOffset;
    private Point? homeSectionPointerStart;
    private CancellationTokenSource? loadingSkeletonPulseCancellation;
    private CancellationTokenSource? transactionLockToastCancellation;
    private bool hasStartedInitialDataLoad;
    private bool isInitialDataLoading;
    private bool isDataLoadingSkeletonShown = true;
    private bool isTransactionEditingLocked = true;

    public MainPage(
        SettingsViewModel settingsViewModel,
        LocalDatabase localDatabase)
    {
        InitializeComponent();
        navigationPages = [DashboardView, ExpensesView, SettingsView];
        navigationIcons = [DashboardIcon, ExpensesIcon, SettingsIcon];
        navigationTabs = [DashboardTab, ExpensesTab, SettingsTab];
        navigationLabels = [DashboardLabel, ExpensesLabel, SettingsLabel];
#if WINDOWS
        var arrangeHomePointerGesture = new PointerGestureRecognizer();
        arrangeHomePointerGesture.PointerPressed += OnHomeSectionPointerPressed;
        arrangeHomePointerGesture.PointerMoved += OnHomeSectionPointerMoved;
        arrangeHomePointerGesture.PointerReleased += OnHomeSectionPointerReleased;
        arrangeHomePointerGesture.PointerExited += OnHomeSectionPointerExited;
        ArrangeHomeSectionsLayout.GestureRecognizers.Add(arrangeHomePointerGesture);
#endif
        UpdateDashboardGreeting();
        this.settingsViewModel = settingsViewModel;
        this.localDatabase = localDatabase;
        LoadHomeSectionOrder();
        ApplyHomeSectionOrder();
        BindingContext = settingsViewModel;
        AddTransactionOverlay.TransactionSaved = OnTransactionSavedAsync;
        AddTransactionOverlay.DatePickerRequested = CalendarPicker.PickAsync;
        AddTransactionOverlay.RunWithTransactionLoadingAsync = RunWithDataLoadingSkeletonAsync;
        ExpensesView.DatePickerRequested = CalendarPicker.PickAsync;
        ExpensesView.RunWithTransactionLoadingAsync = RunWithDataLoadingSkeletonAsync;
        ExpensesView.EditTransactionRequested += OnTransactionEditRequested;
        ExpensesView.DeleteTransactionRequested += OnTransactionDeleteRequested;
        ExpensesView.SearchRequested += OnTransactionSearchRequested;
        ExpensesView.TransactionEditingLockToggleRequested +=
            OnTransactionEditingLockToggleRequested;
        DashboardMonthlySummary.TransactionEditingLockToggleRequested +=
            OnTransactionEditingLockToggleRequested;
        TransactionSearchView.TransactionSelected += OnTransactionSearchResultSelected;
        SettingsView.DataDrawerVisibilityChanged += OnSettingsDataDrawerVisibilityChanged;
        settingsViewModel.PropertyChanged += OnSettingsPropertyChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (hasStartedInitialDataLoad)
        {
            return;
        }

        hasStartedInitialDataLoad = true;
        isInitialDataLoading = true;
        UpdateLoadingSkeletonForSelectedSection();
        // Give the first-frame loader a chance to render before database work
        // begins, including on platforms where initialization resumes inline.
        await Task.Yield();

        try
        {
            await settingsViewModel.InitializeAsync();
            await RefreshTransactionViewsAsync();
        }
        finally
        {
            isInitialDataLoading = false;
            await HideLoadingSkeletonAsync();
        }
    }

    private Task OnTransactionSavedAsync() => RefreshTransactionViewsAsync();

    private async void OnSettingsPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedCurrency))
        {
            if (isInitialDataLoading)
            {
                return;
            }

            if (cachedTransactionRecords is not null)
            {
                await RunWithDataLoadingSkeletonAsync(() =>
                {
                    ApplyTransactionData(
                        cachedTransactionRecords,
                        settingsViewModel.SelectedCurrency);
                    return Task.CompletedTask;
                });
                return;
            }

            await RunWithDataLoadingSkeletonAsync(RefreshTransactionViewsAsync);
        }
    }

    private async void OnDashboardTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(0);

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await NavigateToSectionAsync(2);
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
        var indexOffset = shouldReorder
            ? (int)Math.Round(draggedHomeSectionOffset / rowHeight, MidpointRounding.AwayFromZero)
            : 0;
        var targetIndex = Math.Clamp(startIndex + indexOffset, 0, draftHomeSectionOrder.Count - 1);

        try
        {
            if (targetIndex == startIndex)
            {
                await Task.WhenAll(
                    row.TranslateToAsync(0, 0, 130, Easing.CubicOut),
                    row.ScaleToAsync(1, 130, Easing.CubicOut));
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
            row.TranslationY = 0;
            row.Scale = 1;
            row.Opacity = 1;
            row.ZIndex = 0;
            draggedHomeSectionRow = null;
            draggedHomeSectionKey = null;
            draggedHomeSectionStartIndex = -1;
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

    private async void OnExpensesTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(1);

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(2);

    private async void OnAddTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await AddTransactionOverlay.OpenAsync(
            localDatabase,
            settingsViewModel.SelectedCurrency,
            cachedTransactionRecords ?? []);
        await feedback;
    }

    private async void OnTransactionSearchRequested() =>
        await TransactionSearchView.OpenAsync();

    private async Task OnTransactionSearchResultSelected(int transactionId)
    {
        ShowTransactionLoadingSkeleton();

        try
        {
            // Fade the search surface onto an already-visible skeleton so the
            // search and transaction layouts are never shown together.
            await TransactionSearchView.CloseAsync();

            if (selectedSectionIndex != 1)
            {
                await NavigateToSectionAsync(1);
            }

            await ExpensesView.FocusTransactionAsync(
                transactionId,
                HideLoadingSkeletonAsync);
        }
        finally
        {
            // FocusTransactionAsync also reveals the page before scrolling.
            // This fallback guarantees that a missing or cancelled target can
            // never leave the transition skeleton on screen.
            await HideLoadingSkeletonAsync();
        }
    }

    private void OnSettingsDataDrawerVisibilityChanged(bool isVisible)
    {
        // Keep the dock in its existing native layer to avoid reordering the
        // visual tree while a pointer gesture is completing. The drawer can
        // then cover the same space without the dock drawing or handling input.
        BottomNavigationDock.Opacity = isVisible ? 0 : 1;
        BottomNavigationDock.InputTransparent = isVisible;
    }

    private async void OnTransactionEditRequested(int transactionId)
    {
        if (!isTransactionEditingLocked)
        {
            await OpenTransactionForEditAsync(transactionId);
        }
    }

    private async void OnTransactionDeleteRequested(int transactionId)
    {
        if (!isTransactionEditingLocked)
        {
            await OpenDeleteConfirmationAsync(transactionId);
        }
    }

    private void OnTransactionEditingLockToggleRequested(object? sender, EventArgs e) =>
        SetTransactionEditingLocked(!isTransactionEditingLocked);

    private void SetTransactionEditingLocked(bool isLocked)
    {
        if (isTransactionEditingLocked == isLocked)
        {
            return;
        }

        isTransactionEditingLocked = isLocked;
        DashboardMonthlySummary.IsTransactionEditingLocked = isLocked;
        ExpensesView.SetTransactionEditingLocked(isLocked);

        if (cachedTransactionRecords is not null)
        {
            RefreshDashboard(
                cachedTransactionRecords,
                settingsViewModel.SelectedCurrency);
            Dispatcher.Dispatch(UpdateDashboardTransactionContextMenus);
        }

        _ = ShowTransactionLockToastAsync(isLocked);
    }

    private async Task ShowTransactionLockToastAsync(bool isLocked)
    {
        var previousToast = transactionLockToastCancellation;
        transactionLockToastCancellation = null;
        if (previousToast is not null)
        {
            previousToast.Cancel();
            previousToast.Dispose();
        }

        var cancellation = new CancellationTokenSource();
        transactionLockToastCancellation = cancellation;
        var message = isLocked
            ? "Transactions locked"
            : "Edit and delete enabled";

        TransactionLockToast.CancelAnimations();
        TransactionLockedToastIcon.IsVisible = isLocked;
        TransactionEditingToastIcon.IsVisible = !isLocked;
        TransactionLockToastLabel.Text = message;
        TransactionLockToast.Opacity = 0;
        TransactionLockToast.TranslationY = 10;
        TransactionLockToast.IsVisible = true;
        SemanticScreenReader.Default.Announce(message);

        try
        {
            await Task.WhenAll(
                TransactionLockToast.FadeToAsync(1, 140, Easing.CubicOut),
                TransactionLockToast.TranslateToAsync(0, 0, 170, Easing.CubicOut));
            await Task.Delay(1700, cancellation.Token);
            await Task.WhenAll(
                TransactionLockToast.FadeToAsync(0, 170, Easing.CubicIn),
                TransactionLockToast.TranslateToAsync(0, 8, 170, Easing.CubicIn));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(transactionLockToastCancellation, cancellation))
            {
                TransactionLockToast.CancelAnimations();
                TransactionLockToast.IsVisible = false;
                TransactionLockToast.Opacity = 0;
                TransactionLockToast.TranslationY = 10;
                transactionLockToastCancellation = null;
                cancellation.Dispose();
            }
        }
    }

    private async void OnDashboardEditTransactionInvoked(object? sender, EventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        if (!isTransactionEditingLocked &&
            sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            await OpenTransactionForEditAsync(item.Id);
        }

        await feedback;
    }

    private async void OnDashboardDeleteTransactionInvoked(object? sender, EventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        if (!isTransactionEditingLocked &&
            sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            await OpenDeleteConfirmationAsync(item.Id);
        }

        await feedback;
    }

    private void OnDashboardTransactionRowHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Grid row)
        {
            ConfigureDashboardTransactionContextMenu(row);
        }
    }

    private void UpdateDashboardTransactionContextMenus()
    {
        foreach (var row in DashboardActivityLayout
            .GetVisualTreeDescendants()
            .OfType<Grid>()
            .Where(view => view.ClassId == "TransactionContextMenuTarget"))
        {
            ConfigureDashboardTransactionContextMenu(row);
        }
    }

    private void ConfigureDashboardTransactionContextMenu(Grid row)
    {
#if WINDOWS
        if (row.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement nativeRow)
        {
            return;
        }

        nativeRow.ContextFlyout = null;
        if (isTransactionEditingLocked)
        {
            return;
        }

        var editItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Edit" };
        editItem.Click += async (_, _) =>
        {
            if (!isTransactionEditingLocked &&
                row.BindingContext is TransactionActivityItem item)
            {
                await OpenTransactionForEditAsync(item.Id);
            }
        };

        var deleteItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Delete" };
        deleteItem.Click += async (_, _) =>
        {
            if (!isTransactionEditingLocked &&
                row.BindingContext is TransactionActivityItem item)
            {
                await OpenDeleteConfirmationAsync(item.Id);
            }
        };

        var flyout = new Microsoft.UI.Xaml.Controls.MenuFlyout();
        flyout.Items.Add(editItem);
        flyout.Items.Add(deleteItem);
        nativeRow.ContextFlyout = flyout;
#endif
    }

    private async Task<TransactionRecord?> FindTransactionAsync(int transactionId)
    {
        var cachedTransaction = cachedTransactionRecords?
            .FirstOrDefault(transaction => transaction.Id == transactionId);
        if (cachedTransaction is not null)
        {
            return cachedTransaction;
        }

        TransactionRecord? storedTransaction = null;
        await RunWithDataLoadingSkeletonAsync(async () =>
        {
            storedTransaction = await localDatabase.GetTransactionAsync(transactionId);
            if (storedTransaction is null)
            {
                await RefreshTransactionViewsAsync();
            }
        });

        return storedTransaction;
    }

    private async Task OpenTransactionForEditAsync(int transactionId)
    {
        if (isTransactionEditingLocked)
        {
            return;
        }

        var transaction = await FindTransactionAsync(transactionId);
        if (transaction is null)
        {
            return;
        }

        await AddTransactionOverlay.OpenAsync(
            localDatabase,
            settingsViewModel.SelectedCurrency,
            cachedTransactionRecords ?? [],
            transaction);
    }

    private async Task OpenDeleteConfirmationAsync(int transactionId)
    {
        if (isTransactionEditingLocked ||
            DeleteConfirmationOverlay.IsVisible ||
            isDeleteConfirmationAnimating)
        {
            return;
        }

        var transaction = await FindTransactionAsync(transactionId);
        if (transaction is null)
        {
            return;
        }

        pendingDeleteTransaction = transaction;
        var transactionActivity = TransactionActivityItem.FromRecord(
            transaction,
            settingsViewModel.SelectedCurrency.Symbol);
        DeleteCategoryIcon.Source = transactionActivity.IconAsset;
        DeletePaymentMethodIcon.Source = transactionActivity.PaymentMethodIconAsset;
        var transactionDate = transaction.TransactionDate.ToString(
            "dddd, d MMMM yyyy",
            CultureInfo.CurrentCulture);
        DeleteDescriptionLabel.Text =
            $"“{transaction.Description} on {transactionDate}” will be permanently removed. This cannot be undone.";
        UpdateDeleteConfirmationCardWidth();
        DeleteConfirmLabel.Text = "Delete";
        DeleteConfirmButton.IsEnabled = true;
        DeleteConfirmationOverlay.IsVisible = true;
        DeleteConfirmationOverlay.Opacity = 0;
        DeleteConfirmationCard.Opacity = 0;
        DeleteConfirmationCard.Scale = 0.96;
        DeleteConfirmationCard.TranslationY = 16;
        isDeleteConfirmationAnimating = true;

        try
        {
            await Task.WhenAll(
                DeleteConfirmationOverlay.FadeToAsync(1, 150, Easing.CubicOut),
                DeleteConfirmationCard.FadeToAsync(1, 180, Easing.CubicOut),
                DeleteConfirmationCard.ScaleToAsync(1, 210, Easing.CubicOut),
                DeleteConfirmationCard.TranslateToAsync(0, 0, 210, Easing.CubicOut));
        }
        finally
        {
            isDeleteConfirmationAnimating = false;
        }
    }

    private async void OnDeleteConfirmationBackdropTapped(object? sender, TappedEventArgs e) =>
        await CloseDeleteConfirmationAsync();

    private async void OnDeleteConfirmationCancelTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseDeleteConfirmationAsync();
        await feedback;
    }

    private async void OnDeleteConfirmationConfirmedTapped(object? sender, TappedEventArgs e)
    {
        if (isTransactionEditingLocked ||
            pendingDeleteTransaction is null ||
            isDeletingTransaction)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        isDeletingTransaction = true;
        DeleteConfirmButton.IsEnabled = false;
        DeleteConfirmButton.Opacity = 0.72;
        DeleteConfirmLabel.Text = "Deleting…";

        try
        {
            var transactionId = pendingDeleteTransaction.Id;
            await RunWithDataLoadingSkeletonAsync(async () =>
            {
                await localDatabase.DeleteTransactionAsync(transactionId);
                isDeletingTransaction = false;
                await CloseDeleteConfirmationAsync();
                await RefreshTransactionViewsAsync();
            });
        }
        catch
        {
            DeleteConfirmLabel.Text = "Try again";
            await Task.Delay(900);
        }
        finally
        {
            isDeletingTransaction = false;
            DeleteConfirmButton.IsEnabled = true;
            DeleteConfirmButton.Opacity = 1;
            if (DeleteConfirmationOverlay.IsVisible)
            {
                DeleteConfirmLabel.Text = "Delete";
            }

            await feedback;
        }
    }

    private async Task CloseDeleteConfirmationAsync()
    {
        if (!DeleteConfirmationOverlay.IsVisible ||
            isDeleteConfirmationAnimating ||
            isDeletingTransaction)
        {
            return;
        }

        isDeleteConfirmationAnimating = true;
        try
        {
            await Task.WhenAll(
                DeleteConfirmationOverlay.FadeToAsync(0, 130, Easing.CubicIn),
                DeleteConfirmationCard.ScaleToAsync(0.97, 150, Easing.CubicIn),
                DeleteConfirmationCard.TranslateToAsync(0, 12, 150, Easing.CubicIn));
        }
        finally
        {
            DeleteConfirmationOverlay.IsVisible = false;
            DeleteConfirmationOverlay.Opacity = 0;
            DeleteConfirmationCard.Opacity = 1;
            DeleteConfirmationCard.Scale = 1;
            DeleteConfirmationCard.TranslationY = 0;
            pendingDeleteTransaction = null;
            isDeleteConfirmationAnimating = false;
        }
    }

    private void OnDeleteConfirmationOverlaySizeChanged(object? sender, EventArgs e) =>
        UpdateDeleteConfirmationCardWidth();

    private void UpdateDeleteConfirmationCardWidth()
    {
        var availableWidth = DeleteConfirmationOverlay.Width > 0
            ? DeleteConfirmationOverlay.Width
            : Width;
        if (availableWidth > 0)
        {
            DeleteConfirmationCard.WidthRequest = Math.Min(
                420,
                Math.Max(280, availableWidth - 40));
        }
    }

    private void OnNavigationTabsSizeChanged(object? sender, EventArgs e)
    {
        NavigationSelectionPill.TranslationX = GetSelectionPillOffset(selectedSectionIndex);
    }

    private async Task NavigateToSectionAsync(int selectedIndex)
    {
        if (selectedIndex == 0)
        {
            UpdateDashboardGreeting();
        }

        var selectedIcon = navigationIcons[selectedIndex];

        if (selectedIndex == selectedSectionIndex)
        {
            await AnimateNavigationIconAsync(selectedIcon);
            return;
        }

        var transitionVersion = ++navigationTransitionVersion;
        var previousIndex = selectedSectionIndex;
        var previousPage = navigationPages[previousIndex];
        var selectedPage = navigationPages[selectedIndex];

        foreach (var page in navigationPages)
        {
            page.CancelAnimations();
            if (page != previousPage && page != selectedPage)
            {
                page.IsVisible = false;
                page.Opacity = 1;
                page.TranslationY = 0;
            }
        }

        selectedSectionIndex = selectedIndex;
        previousPage.IsVisible = false;
        previousPage.Opacity = 1;
        previousPage.TranslationY = 0;
        selectedPage.IsVisible = true;
        selectedPage.Opacity = 1;
        selectedPage.TranslationY = 0;
        UpdateNavigationStyles(selectedIndex);
        UpdateLoadingSkeletonForSelectedSection();

        NavigationSelectionPill.CancelAnimations();
        var pillOffset = GetSelectionPillOffset(selectedIndex);

        await Task.WhenAll(
            NavigationSelectionPill.TranslateToAsync(
                pillOffset,
                0,
                190,
                Easing.CubicOut),
            AnimateNavigationIconAsync(selectedIcon));

        if (transitionVersion != navigationTransitionVersion)
        {
            return;
        }

        selectedPage.Opacity = 1;
        selectedPage.TranslationY = 0;
    }

    private void UpdateDashboardGreeting()
    {
        DashboardGreetingLabel.Text = DateTime.Now.Hour switch
        {
            >= 5 and < 12 => "Good morning,",
            >= 12 and < 17 => "Good afternoon,",
            _ => "Good evening,"
        };
    }

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

    private async Task RefreshTransactionViewsAsync()
    {
        await transactionRefreshLock.WaitAsync();
        try
        {
            var records = await localDatabase.GetTransactionsAsync();
            cachedTransactionRecords = records;
            ApplyTransactionData(records, settingsViewModel.SelectedCurrency);
        }
        finally
        {
            transactionRefreshLock.Release();
        }
    }

    private void ApplyTransactionData(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption currency)
    {
        ExpensesView.Refresh(records, currency);
        TransactionSearchView.SetTransactions(records, currency);
        RefreshDashboard(records, currency);
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
            DateTime.Today);

        TodaySpentValueLabel.Text = summary.TodaySpentText;
        DailyAverageValueLabel.Text = summary.DailyAverageText;
        DaysRemainingValueLabel.Text = summary.DaysRemainingText;
        ExpenseCategoryTotalLabel.Text = summary.ExpenseTotalText;
        IncomeCategoryTotalLabel.Text = summary.IncomeTotalText;
        DashboardInsightLabel.Text = summary.InsightText;
        BindableLayout.SetItemsSource(
            DashboardExpenseCategoriesLayout,
            summary.ExpenseCategories);
        BindableLayout.SetItemsSource(
            DashboardIncomeCategoriesLayout,
            summary.IncomeCategories);
        BindableLayout.SetItemsSource(
            DashboardActivityLayout,
            summary.RecentActivity);
        var hasRecentActivity = summary.RecentActivity.Count > 0;
        DashboardActivityCard.IsVisible = hasRecentActivity;
        DashboardEmptyActivityState.IsVisible = !hasRecentActivity;
        DashboardViewAllButton.IsVisible = hasRecentActivity;
    }

    private double GetSelectionPillOffset(int selectedIndex)
    {
        return navigationTabs[selectedIndex].X - ExpensesTab.X;
    }

    private static async Task AnimateNavigationIconAsync(VisualElement icon)
    {
        icon.CancelAnimations();
        icon.Scale = 1;
        icon.TranslationY = 0;

        await Task.WhenAll(
            icon.ScaleToAsync(1.1, 85, Easing.CubicOut),
            icon.TranslateToAsync(0, -2, 85, Easing.CubicOut));
        await Task.WhenAll(
            icon.ScaleToAsync(1, 135, Easing.SpringOut),
            icon.TranslateToAsync(0, 0, 135, Easing.CubicOut));
    }

    private void UpdateNavigationStyles(int selectedIndex)
    {
        var resources = Application.Current!.Resources;

        for (var index = 0; index < navigationTabs.Length; index++)
        {
            var isSelected = index == selectedIndex;
            navigationTabs[index].Style = (Style)resources["NavTab"];
            navigationLabels[index].Style = (Style)resources[
                isSelected ? "SelectedNavLabel" : "NavLabel"];

            navigationIcons[index].Style = (Style)resources[
                isSelected ? "SelectedNavIcon" : "NavIcon"];
        }
    }
}
