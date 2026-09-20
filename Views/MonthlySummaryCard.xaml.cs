using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

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

    private string availableAmountText = "RM 0.00";
    private string incomeAmountText = "RM 0.00";
    private string expenseAmountText = "RM 0.00";
    private bool areAmountsVisible = true;

    public MonthlySummaryCard()
    {
        InitializeComponent();
        UpdateTransactionLockVisuals();
    }

    public event EventHandler? TransactionEditingLockToggleRequested;

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
        long incomeMinor = 0;
        long expenseMinor = 0;
        var transactionCount = 0;

        foreach (var record in records)
        {
            if (!record.CurrencyCode.Equals(selectedCurrency.Code, StringComparison.OrdinalIgnoreCase) ||
                record.TransactionDate.Date < startDate ||
                record.TransactionDate.Date > endDate)
            {
                continue;
            }

            transactionCount++;
            if (record.Type.Equals("Income", StringComparison.OrdinalIgnoreCase))
            {
                incomeMinor += record.AmountMinor;
            }
            else
            {
                expenseMinor += record.AmountMinor;
            }
        }

        SummaryMonthLabel.Text = title;
        SummaryAvailableCaptionLabel.Text = availableCaption;
        availableAmountText = MoneyFormatter.FormatMinor(
            incomeMinor - expenseMinor,
            selectedCurrency.Symbol,
            separateSign: true);
        incomeAmountText = MoneyFormatter.FormatMinor(incomeMinor, selectedCurrency.Symbol);
        expenseAmountText = MoneyFormatter.FormatMinor(expenseMinor, selectedCurrency.Symbol);
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
