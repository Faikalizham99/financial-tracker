using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FinancialTracker.Views;

public partial class MonthlySummaryCard : ContentView
{
    public static readonly BindableProperty ShowTransactionCountProperty =
        BindableProperty.Create(
            nameof(ShowTransactionCount),
            typeof(bool),
            typeof(MonthlySummaryCard),
            false,
            propertyChanged: static (bindable, _, newValue) =>
                ((MonthlySummaryCard)bindable).TransactionCountPanel.IsVisible = (bool)newValue);

    public static readonly BindableProperty IsTransactionEditingLockedProperty =
        BindableProperty.Create(
            nameof(IsTransactionEditingLocked),
            typeof(bool),
            typeof(MonthlySummaryCard),
            true,
            propertyChanged: static (bindable, _, _) =>
                ((MonthlySummaryCard)bindable).UpdateTransactionLockVisuals());

    public static readonly BindableProperty IncludeInvestmentInTotalsProperty =
        BindableProperty.Create(
            nameof(IncludeInvestmentInTotals),
            typeof(bool),
            typeof(MonthlySummaryCard),
            true,
            propertyChanged: static (bindable, _, _) =>
            {
                var card = (MonthlySummaryCard)bindable;
                card.UpdateInvestmentToggleVisuals(card.IsLoaded);
                card.RefreshCachedSummary();
            });

    private string availableAmountText = "RM 0.00";
    private string incomeAmountText = "RM 0.00";
    private string expenseAmountText = "RM 0.00";
    private string budgetUsageAmountText = "RM 0.00 / RM 0.00";
    private string budgetUsagePercentageText = "0% USED";
    private string budgetUsageRemainingText = "RM 0.00 left";
    private string budgetSurplusText = string.Empty;
    private bool hasBudgetSurplus;
    private Func<DateTime, Task<MonthlyBudgetRecord?>>? budgetProvider;
    private bool areAmountsVisible = true;
    private IReadOnlyList<TransactionRecord>? cachedRecords;
    private CurrencyOption? cachedCurrency;
    private MonthlyBudgetRecord? cachedBudget;
    private DateTime? cachedBudgetMonth;
    private DateTime cachedStartDate;
    private DateTime cachedEndDate;
    private int budgetLoadVersion;

    public MonthlySummaryCard()
    {
        InitializeComponent();
        UpdateTransactionLockVisuals();
        UpdateInvestmentToggleVisuals(animate: false);
    }

    public event EventHandler? TransactionEditingLockToggleRequested;
    public event EventHandler? InvestmentInclusionToggleRequested;

    public bool ShowTransactionCount
    {
        get => (bool)GetValue(ShowTransactionCountProperty);
        set => SetValue(ShowTransactionCountProperty, value);
    }

    public bool IsTransactionEditingLocked
    {
        get => (bool)GetValue(IsTransactionEditingLockedProperty);
        set => SetValue(IsTransactionEditingLockedProperty, value);
    }

    public bool IncludeInvestmentInTotals
    {
        get => (bool)GetValue(IncludeInvestmentInTotalsProperty);
        set => SetValue(IncludeInvestmentInTotalsProperty, value);
    }

    public Func<DateTime, Task<MonthlyBudgetRecord?>>? BudgetProvider
    {
        get => budgetProvider;
        set => budgetProvider = value;
    }

    public void Refresh(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency,
        DateTime? displayedMonth = null)
    {
        var month = displayedMonth ?? DateTime.Today;
        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        var requestVersion = PrepareBudgetMonth(normalizedMonth);
        RefreshCore(
            records,
            selectedCurrency,
            normalizedMonth,
            normalizedMonth.AddMonths(1).AddDays(-1),
            month.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            $"Available for {month.ToString("MMMM", CultureInfo.CurrentCulture)}");
        if (requestVersion is int version)
        {
            _ = LoadBudgetAsync(normalizedMonth, version);
        }
    }

    public void RefreshRange(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency,
        DateTime startDate,
        DateTime endDate)
    {
        ClearBudgetContext();
        var normalizedStart = startDate.Date;
        var normalizedEnd = endDate.Date;
        if (normalizedStart > normalizedEnd)
        {
            (normalizedStart, normalizedEnd) = (normalizedEnd, normalizedStart);
        }

        RefreshCore(
            records,
            selectedCurrency,
            normalizedStart,
            normalizedEnd,
            FormatRangeTitle(normalizedStart, normalizedEnd),
            "Available for selected range");
    }

    public void ReloadBudget()
    {
        if (cachedBudgetMonth is not DateTime month)
        {
            return;
        }

        var requestVersion = ++budgetLoadVersion;
        _ = LoadBudgetAsync(month, requestVersion);
    }

    private void RefreshCore(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency,
        DateTime startDate,
        DateTime endDate,
        string title,
        string availableCaption)
    {
        cachedRecords = records;
        cachedCurrency = selectedCurrency;
        cachedStartDate = startDate;
        cachedEndDate = endDate;
        SummaryMonthLabel.Text = title;
        SummaryAvailableCaptionLabel.Text = availableCaption;
        RefreshCachedSummary();
    }

    private void RefreshCachedSummary()
    {
        if (cachedRecords is null || cachedCurrency is null)
        {
            return;
        }

        long incomeMinor = 0;
        long budgetIncomeMinor = 0;
        long expenseMinor = 0;
        var transactionCount = 0;

        foreach (var record in cachedRecords)
        {
            if (record.TransactionDate.Date < cachedStartDate ||
                record.TransactionDate.Date > cachedEndDate)
            {
                continue;
            }

            transactionCount++;
            if (!IncludeInvestmentInTotals &&
                record.Category.Equals(
                    TransactionCatalog.InvestmentCategoryKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (record.Type.Equals("Income", StringComparison.OrdinalIgnoreCase))
            {
                incomeMinor += record.AmountMinor;
                if (!TransactionCatalog.IsIncomeExcludedFromBudgetUsage(record.Category))
                {
                    budgetIncomeMinor += record.AmountMinor;
                }
            }
            else
            {
                expenseMinor += record.AmountMinor;
            }
        }

        availableAmountText = MoneyFormatter.FormatMinor(
            incomeMinor - expenseMinor,
            cachedCurrency.Symbol,
            separateSign: true);
        incomeAmountText = MoneyFormatter.FormatMinor(incomeMinor, cachedCurrency.Symbol);
        expenseAmountText = MoneyFormatter.FormatMinor(
            expenseMinor,
            cachedCurrency.Symbol);
        UpdateBudgetProgress(expenseMinor, budgetIncomeMinor, cachedCurrency.Symbol);

        SummaryTransactionCountLabel.Text = transactionCount.ToString(
            CultureInfo.InvariantCulture);
        UpdateAmountVisibility();
    }

    private void UpdateBudgetProgress(
        long expenseMinor,
        long offsetIncomeMinor,
        string currencySymbol)
    {
        if (cachedBudget is null)
        {
            BudgetProgressSection.IsVisible = false;
            return;
        }

        var budgetMinor = IncludeInvestmentInTotals
            ? cachedBudget.BudgetIncludingInvestmentMinor
            : cachedBudget.BudgetExcludingInvestmentMinor;
        if (budgetMinor <= 0)
        {
            BudgetProgressSection.IsVisible = false;
            return;
        }

        var netSpendingMinor = Math.Max(expenseMinor - offsetIncomeMinor, 0);
        var surplusMinor = Math.Max(offsetIncomeMinor - expenseMinor, 0);
        var usageRatio = (double)netSpendingMinor / budgetMinor;
        var remainingMinor = budgetMinor - netSpendingMinor;
        budgetUsageAmountText =
            $"{MoneyFormatter.FormatMinor(netSpendingMinor, currencySymbol)} / " +
            MoneyFormatter.FormatMinor(budgetMinor, currencySymbol);
        budgetUsagePercentageText =
            $"{Math.Max(0, usageRatio * 100):0.#}% USED";
        hasBudgetSurplus = surplusMinor > 0;
        budgetSurplusText = hasBudgetSurplus
            ? $"{MoneyFormatter.FormatMinor(surplusMinor, currencySymbol)} surplus"
            : string.Empty;
        budgetUsageRemainingText = remainingMinor >= 0
            ? $"{MoneyFormatter.FormatMinor(remainingMinor, currencySymbol)} left"
            : $"{MoneyFormatter.FormatMinor(-remainingMinor, currencySymbol)} over budget";
        BudgetUsageProgress.Progress = Math.Clamp(usageRatio, 0, 1);
        BudgetProgressSection.IsVisible = true;

        if (usageRatio > 1)
        {
            ThemeResourceBindings.SetColor(
                BudgetUsageProgress,
                ProgressBar.ProgressColorProperty,
                "NegativeLight",
                "NegativeDark");
            ThemeResourceBindings.SetColor(
                BudgetUsagePercentageLabel,
                Label.TextColorProperty,
                "NegativeLight",
                "NegativeDark");
        }
        else
        {
            ThemeResourceBindings.SetDynamic(
                BudgetUsageProgress,
                ProgressBar.ProgressColorProperty,
                "Accent");
            ThemeResourceBindings.SetDynamic(
                BudgetUsagePercentageLabel,
                Label.TextColorProperty,
                "Accent");
        }
    }

    private int? PrepareBudgetMonth(DateTime month)
    {
        if (cachedBudgetMonth == month)
        {
            return null;
        }

        cachedBudgetMonth = month;
        cachedBudget = null;
        return ++budgetLoadVersion;
    }

    private void ClearBudgetContext()
    {
        budgetLoadVersion++;
        cachedBudgetMonth = null;
        cachedBudget = null;
    }

    private async Task LoadBudgetAsync(DateTime month, int requestVersion)
    {
        if (BudgetProvider is null)
        {
            return;
        }

        MonthlyBudgetRecord? budget = null;
        try
        {
            budget = await BudgetProvider(month);
        }
        catch
        {
            // Budget display is supplemental; transaction totals remain usable.
        }

        if (requestVersion != budgetLoadVersion || cachedBudgetMonth != month)
        {
            return;
        }

        cachedBudget = budget;
        RefreshCachedSummary();
    }

    private static string FormatRangeTitle(DateTime startDate, DateTime endDate)
    {
        if (startDate == endDate)
        {
            return startDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }

        return startDate.Year == endDate.Year
            ? $"{startDate.ToString("d MMM", CultureInfo.CurrentCulture)} – {endDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}"
            : $"{startDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)} – {endDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";
    }

    private async void OnAmountVisibilityTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(AmountVisibilityButton);
        areAmountsVisible = !areAmountsVisible;
        UpdateAmountVisibility();
        await feedback;
    }

    private async void OnTransactionLockTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(TransactionLockButton);
        TransactionEditingLockToggleRequested?.Invoke(this, EventArgs.Empty);
        await feedback;
    }

    private async void OnInvestmentToggleTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(InvestmentToggleButton);
        InvestmentInclusionToggleRequested?.Invoke(this, EventArgs.Empty);
        await feedback;
    }

    private void UpdateInvestmentToggleVisuals(bool animate)
    {
        if (IncludeInvestmentInTotals)
        {
            ThemeResourceBindings.SetDynamic(
                InvestmentToggleButton,
                BackgroundColorProperty,
                "Accent");
            ThemeResourceBindings.SetDynamic(
                InvestmentToggleIcon,
                Shape.StrokeProperty,
                "Accent");
        }
        else
        {
            ThemeResourceBindings.SetColor(
                InvestmentToggleButton,
                BackgroundColorProperty,
                "SecondaryTextLight",
                "ControlDividerDark");
            ThemeResourceBindings.SetColor(
                InvestmentToggleIcon,
                Shape.StrokeProperty,
                "SecondaryTextLight",
                "SecondaryTextDark");
        }

        var targetTranslation = IncludeInvestmentInTotals ? 22d : 0d;
        InvestmentToggleThumb.CancelAnimations();
        if (animate)
        {
            _ = InvestmentToggleThumb.TranslateToAsync(
                targetTranslation,
                0,
                150,
                Easing.CubicOut);
        }
        else
        {
            InvestmentToggleThumb.TranslationX = targetTranslation;
        }

        SemanticProperties.SetDescription(
            InvestmentToggleButton,
            IncludeInvestmentInTotals
                ? "Investment included in summary totals. Tap to exclude it."
                : "Investment excluded from summary totals. Tap to include it.");
    }

    private void UpdateTransactionLockVisuals()
    {
        LockedTransactionState.IsVisible = IsTransactionEditingLocked;
        UnlockedTransactionState.IsVisible = !IsTransactionEditingLocked;
        if (IsTransactionEditingLocked)
        {
            TransactionLockButton.RemoveDynamicResource(BackgroundColorProperty);
            TransactionLockButton.BackgroundColor = Colors.Transparent;
        }
        else
        {
            TransactionLockButton.SetDynamicResource(
                BackgroundColorProperty,
                "AccentTint");
        }

        SemanticProperties.SetDescription(
            TransactionLockButton,
            IsTransactionEditingLocked
                ? "Transactions locked. Tap to allow editing and deletion."
                : "Transactions unlocked. Tap to prevent editing and deletion.");
    }

    private void UpdateAmountVisibility()
    {
        SummaryAvailableAmountLabel.Text = areAmountsVisible
            ? availableAmountText
            : "••••••";
        SummaryIncomeAmountLabel.Text = areAmountsVisible
            ? incomeAmountText
            : "••••••";
        SummaryExpenseAmountLabel.Text = areAmountsVisible
            ? expenseAmountText
            : "••••••";
        BudgetUsageAmountLabel.Text = areAmountsVisible
            ? budgetUsageAmountText
            : new string('\u2022', 6);
        BudgetUsagePercentageLabel.Text = areAmountsVisible
            ? budgetUsagePercentageText
            : new string('\u2022', 2);
        BudgetSurplusLabel.Text = areAmountsVisible
            ? budgetSurplusText
            : new string('\u2022', 4);
        BudgetSurplusPanel.IsVisible = hasBudgetSurplus && areAmountsVisible;
        BudgetUsageRemainingLabel.Text = areAmountsVisible
            ? budgetUsageRemainingText
            : new string('\u2022', 6);
        BudgetUsageProgress.IsVisible = areAmountsVisible;
        UpdateAmountFontSizes();
        OpenEyeIcon.IsVisible = areAmountsVisible;
        ClosedEyeIcon.IsVisible = !areAmountsVisible;
    }

    private void UpdateAmountFontSizes()
    {
        var isPhone = DeviceInfo.Idiom == DeviceIdiom.Phone;
        var availableLength = SummaryAvailableAmountLabel.Text.Length;

        SummaryAvailableAmountLabel.FontSize = (isPhone, ShowTransactionCount, availableLength) switch
        {
            (true, true, > 14) => 25,
            (true, true, > 10) => 29,
            (true, true, _) => 34,
            (true, false, > 16) => 30,
            (true, false, > 12) => 34,
            (true, false, _) => 38,
            (false, _, > 18) => 30,
            (false, _, > 14) => 35,
            _ => 40
        };

        SummaryExpenseAmountLabel.FontSize = 21;
        var budgetLength = BudgetUsageAmountLabel.Text.Length;
        BudgetUsageAmountLabel.FontSize = (isPhone, budgetLength) switch
        {
            (true, > 32) => 13,
            (true, > 26) => 15,
            (true, > 20) => 17,
            (true, _) => 21,
            (false, > 38) => 16,
            (false, > 30) => 18,
            _ => 22
        };
    }

}
