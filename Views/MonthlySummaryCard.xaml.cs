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
    private bool areAmountsVisible = true;
    private IReadOnlyList<TransactionRecord>? cachedRecords;
    private CurrencyOption? cachedCurrency;
    private DateTime cachedStartDate;
    private DateTime cachedEndDate;

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

    public void Refresh(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency,
        DateTime? displayedMonth = null)
    {
        var month = displayedMonth ?? DateTime.Today;
        RefreshCore(
            records,
            selectedCurrency,
            new DateTime(month.Year, month.Month, 1),
            new DateTime(month.Year, month.Month, 1).AddMonths(1).AddDays(-1),
            month.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            $"Available for {month.ToString("MMMM", CultureInfo.CurrentCulture)}");
    }

    public void RefreshRange(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency,
        DateTime startDate,
        DateTime endDate)
    {
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
        expenseAmountText = MoneyFormatter.FormatMinor(expenseMinor, cachedCurrency.Symbol);
        SummaryTransactionCountLabel.Text = transactionCount.ToString(
            CultureInfo.InvariantCulture);
        UpdateAmountVisibility();
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
    }

}
