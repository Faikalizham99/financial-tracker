using System.ComponentModel;
using FinancialTracker.Data;
using FinancialTracker.Models;
using FinancialTracker.Services;
using FinancialTracker.ViewModels;
using Microsoft.Maui.Storage;

namespace FinancialTracker;

public partial class MainPage : ContentPage
{
    private const string HomeSectionOrderPreferenceKey = "home_section_order";
    private const string IncludeInvestmentInTotalsPreferenceKey =
        "include_investment_in_summary_totals";
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
    private readonly MonthlyBudgetService monthlyBudgetService;
    private readonly TransactionDataStore transactionDataStore;
    private readonly WidgetSnapshotCoordinator widgetSnapshotCoordinator;
    private readonly IWidgetSettingsStore widgetSettingsStore;
    private readonly LocalDatabase localDatabase;
    private readonly IBackupFileSaver backupFileSaver;
    private readonly IBackupFilePicker backupFilePicker;
    private readonly SemaphoreSlim initialDataLoadLock = new(1, 1);
    private readonly SemaphoreSlim dataLoadingOperationLock = new(1, 1);
    private readonly List<string> homeSectionOrder = [];
    private readonly List<string> draftHomeSectionOrder = [];
    private IReadOnlyList<TransactionActivityItem> dashboardRecentActivity = [];
    private readonly VisualElement[] navigationPages;
    private readonly VisualElement[] navigationIcons;
    private readonly Border[] navigationTabs;
    private readonly Label[] navigationLabels;
    private int selectedSectionIndex = 1;
    private int navigationTransitionVersion;
    private int? expandedDashboardTransactionDescriptionId;
    private TransactionRecord? pendingDeleteTransaction;
    private bool isDeleteConfirmationAnimating;
    private bool isDeletingTransaction;
    private bool isArrangeHomeOpen;
    private bool isArrangeHomeAnimating;
    private bool isHomeSectionReordering;
    private Grid? draggedHomeSectionRow;
    private string? draggedHomeSectionKey;
    private int draggedHomeSectionStartIndex = -1;
    private int draggedHomeSectionTargetIndex = -1;
    private double draggedHomeSectionOffset;
    private Point? homeSectionPointerStart;
    private CancellationTokenSource? transactionLockToastCancellation;
    private bool hasCompletedInitialDataLoad;
    private bool isInitialDataLoading;
    private bool isDataLoadingSkeletonShown = true;
    private bool isTransactionEditingLocked = true;
    private bool includeInvestmentInTotals = true;

    public MainPage(
        SettingsViewModel settingsViewModel,
        BudgetSettingsViewModel budgetSettingsViewModel,
        TransactionStatisticsViewModel transactionStatisticsViewModel,
        TransactionStatisticsDetailViewModel transactionStatisticsDetailViewModel,
        MonthlyBudgetService monthlyBudgetService,
        AssetPortfolioService assetPortfolioService,
        TransactionDataStore transactionDataStore,
        WidgetSnapshotCoordinator widgetSnapshotCoordinator,
        IWidgetSettingsStore widgetSettingsStore,
        LocalDatabase localDatabase,
        IBackupFileSaver backupFileSaver,
        IBackupFilePicker backupFilePicker)
    {
        InitializeComponent();
        SelectLoadingSkeleton(selectedSectionIndex);
        navigationPages = [DashboardView, ExpensesView, AssetsView, SettingsView];
        navigationIcons = [DashboardIcon, ExpensesIcon, AssetsIcon, SettingsIcon];
        navigationTabs = [DashboardTab, ExpensesTab, AssetsTab, SettingsTab];
        navigationLabels = [DashboardLabel, ExpensesLabel, AssetsLabel, SettingsLabel];
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
        this.monthlyBudgetService = monthlyBudgetService;
        this.transactionDataStore = transactionDataStore;
        this.widgetSnapshotCoordinator = widgetSnapshotCoordinator;
        this.widgetSettingsStore = widgetSettingsStore;
        this.localDatabase = localDatabase;
        this.backupFileSaver = backupFileSaver;
        this.backupFilePicker = backupFilePicker;
        LoadHomeSectionOrder();
        ApplyHomeSectionOrder();
        BindingContext = settingsViewModel;
        BudgetSettingsOverlay.BindingContext = budgetSettingsViewModel;
        TransactionStatisticsOverlay.ConfigureDetailView(transactionStatisticsDetailViewModel);
        TransactionStatisticsOverlay.BindingContext = transactionStatisticsViewModel;
        DashboardMonthlySummary.BudgetProvider = monthlyBudgetService.GetAsync;
        ExpensesView.SetBudgetProvider(monthlyBudgetService.GetAsync);
        AssetsView.Configure(assetPortfolioService, () => settingsViewModel.SelectedCurrency);
        AddTransactionOverlay.TransactionSaved = OnTransactionSavedAsync;
        AddTransactionOverlay.DatePickerRequested = CalendarPicker.PickAsync;
        AddTransactionOverlay.RunWithTransactionLoadingAsync = RunWithDataLoadingSkeletonAsync;
        ExpensesView.DatePickerRequested = CalendarPicker.PickAsync;
        AssetsView.DatePickerRequested = CalendarPicker.PickAsync;
        ExpensesView.RunWithTransactionLoadingAsync = RunWithDataLoadingSkeletonAsync;
        ExpensesView.TransactionsRequested = transactionDataStore.GetPeriodAsync;
        ExpensesView.EditTransactionRequested += OnTransactionEditRequested;
        ExpensesView.QuickEditTransactionRequested += OnTransactionQuickEditRequested;
        ExpensesView.DeleteTransactionRequested += OnTransactionDeleteRequested;
        ExpensesView.SearchRequested += OnTransactionSearchRequested;
        ExpensesView.StatisticsRequested += OnTransactionStatisticsRequested;
        ExpensesView.TransactionEditingLockToggleRequested +=
            OnTransactionEditingLockToggleRequested;
        DashboardMonthlySummary.TransactionEditingLockToggleRequested +=
            OnTransactionEditingLockToggleRequested;
        DashboardMonthlySummary.InvestmentInclusionToggleRequested +=
            OnInvestmentInclusionToggleRequested;
        ExpensesView.InvestmentInclusionToggleRequested +=
            OnInvestmentInclusionToggleRequested;
        TransactionSearchView.SearchPageRequested = transactionDataStore.SearchAsync;
        TransactionSearchView.TransactionSelected += OnTransactionSearchResultSelected;
        SettingsView.DataDrawerVisibilityChanged += OnSettingsDataDrawerVisibilityChanged;
        AssetsView.EditorVisibilityChanged += OnAssetsEditorVisibilityChanged;
        SettingsView.BudgetSettingsRequested += OnBudgetSettingsRequested;
        BudgetSettingsOverlay.VisibilityChanged += OnBudgetSettingsVisibilityChanged;
        TransactionStatisticsOverlay.VisibilityChanged +=
            OnTransactionStatisticsVisibilityChanged;
        TransactionStatisticsOverlay.TransactionEditRequested +=
            OnTransactionStatisticsEditRequested;
        budgetSettingsViewModel.BudgetSaved += OnBudgetSaved;
        SettingsView.DatabaseBackupRequested += OnDatabaseBackupRequested;
        SettingsView.DatabaseRestoreRequested += OnDatabaseRestoreRequested;
        SettingsView.DatabaseResetRequested += OnDatabaseResetRequested;
        settingsViewModel.PropertyChanged += OnSettingsPropertyChanged;
        var savedIncludeInvestment = Preferences.Default.Get(
            IncludeInvestmentInTotalsPreferenceKey,
            true);
        if (widgetSettingsStore.TryReadIncludeInvestment(
                out var sharedIncludeInvestment))
        {
            includeInvestmentInTotals = sharedIncludeInvestment;
            Preferences.Default.Set(
                IncludeInvestmentInTotalsPreferenceKey,
                sharedIncludeInvestment);
        }
        else
        {
            includeInvestmentInTotals = savedIncludeInvestment;
            widgetSettingsStore.TryWriteIncludeInvestment(
                savedIncludeInvestment);
        }

        UpdateInvestmentInclusionState();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        try
        {
            await EnsureInitialDataLoadedAsync();
        }
        catch
        {
            await DisplayAlertAsync(
                "Data unavailable",
                "Financial Tracker could not load your saved data. Please try opening the app again.",
                "OK");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (CalendarPicker.IsOpen)
        {
            _ = CalendarPicker.DismissAsync();
            return true;
        }

        if (AssetsView.HasOpenOverlay)
        {
            _ = AssetsView.HandleBackAsync();
            return true;
        }

        if (TransactionStatisticsOverlay.IsOpen)
        {
            _ = TransactionStatisticsOverlay.HandleBackAsync();
            return true;
        }

        if (BudgetSettingsOverlay.IsOpen)
        {
            _ = BudgetSettingsOverlay.HandleBackAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async Task EnsureInitialDataLoadedAsync()
    {
        await initialDataLoadLock.WaitAsync();
        var completedSuccessfully = false;
        try
        {
            if (hasCompletedInitialDataLoad)
            {
                return;
            }

            isInitialDataLoading = true;
            UpdateLoadingSkeletonForSelectedSection();
            // Give the first-frame loader a chance to render before database work
            // begins, including on platforms where initialization resumes inline.
            await Task.Yield();

            var snapshot = await transactionDataStore.LoadStartupAsync(DateTime.Today);
            await settingsViewModel.InitializeAsync();
            ApplyTransactionData(snapshot, settingsViewModel.SelectedCurrency);
            hasCompletedInitialDataLoad = true;
            completedSuccessfully = true;
        }
        finally
        {
            try
            {
                isInitialDataLoading = false;
                if (completedSuccessfully || hasCompletedInitialDataLoad)
                {
                    await HideLoadingSkeletonAsync();
                }
            }
            finally
            {
                initialDataLoadLock.Release();
            }
        }
    }

    internal async Task RefreshAfterResumeAsync()
    {
        SynchronizeInvestmentInclusionFromWidget();
        if (!hasCompletedInitialDataLoad)
        {
            await EnsureInitialDataLoadedAsync();
            return;
        }

        await RunWithDataLoadingSkeletonAsync(async () =>
        {
            monthlyBudgetService.InvalidateCache();
            await settingsViewModel.RefreshCurrentBudgetStatusAsync();
            await RefreshTransactionViewsAfterResumeAsync();
            await ReloadMonthlyBudgetCardsAsync();
        });
    }

    private async Task OnTransactionSavedAsync()
    {
        await RefreshTransactionViewsAsync();
        await TransactionStatisticsOverlay.RefreshAsync(
            settingsViewModel.SelectedCurrency,
            includeInvestmentInTotals);
    }

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

            if (transactionDataStore.IsLoaded)
            {
                var currency = settingsViewModel.SelectedCurrency;
                ExpensesView.SetCurrency(currency);
                TransactionSearchView.SetCurrency(currency);
                RefreshDashboard(
                    transactionDataStore.DashboardRecords,
                    currency);
                AssetsView.InvalidateCurrency();
                return;
            }

            await RefreshTransactionViewsAsync();
            AssetsView.InvalidateCurrency();
        }
    }
}
