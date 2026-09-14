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

    private string availableAmountText = "RM 0.00";
    private string incomeAmountText = "RM 0.00";
    private string expenseAmountText = "RM 0.00";
    private bool areAmountsVisible = true;

    public MonthlySummaryCard()
    {
        InitializeComponent();
    }

    public bool ShowTransactionCount
    {
        get => (bool)GetValue(ShowTransactionCountProperty);
        set => SetValue(ShowTransactionCountProperty, value);
    }

    public void Refresh(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency,
        DateTime? displayedMonth = null)
    {
        var month = displayedMonth ?? DateTime.Today;
        var currentMonthRecords = records
            .Where(record =>
                record.CurrencyCode.Equals(selectedCurrency.Code, StringComparison.OrdinalIgnoreCase) &&
                record.TransactionDate.Year == month.Year &&
                record.TransactionDate.Month == month.Month)
            .ToList();
        var incomeMinor = currentMonthRecords
            .Where(record => record.Type.Equals("Income", StringComparison.OrdinalIgnoreCase))
            .Sum(record => record.AmountMinor);
        var expenseMinor = currentMonthRecords
            .Where(record => record.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase))
            .Sum(record => record.AmountMinor);

        SummaryMonthLabel.Text = month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        SummaryAvailableCaptionLabel.Text =
            $"Available for {month.ToString("MMMM", CultureInfo.CurrentCulture)}";
        availableAmountText = FormatMoney(
            selectedCurrency,
            incomeMinor - expenseMinor);
        incomeAmountText = FormatMoney(selectedCurrency, incomeMinor);
        expenseAmountText = FormatMoney(selectedCurrency, expenseMinor);
        SummaryTransactionCountLabel.Text = currentMonthRecords.Count.ToString(
            CultureInfo.InvariantCulture);
        UpdateAmountVisibility();
    }

    private async void OnAmountVisibilityTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(AmountVisibilityButton);
        areAmountsVisible = !areAmountsVisible;
        UpdateAmountVisibility();
        await feedback;
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
        OpenEyeIcon.IsVisible = areAmountsVisible;
        ClosedEyeIcon.IsVisible = !areAmountsVisible;
    }

    private static string FormatMoney(CurrencyOption currency, long amountMinor)
    {
        var sign = amountMinor < 0 ? "− " : string.Empty;
        var amount = Math.Abs(amountMinor) / 100m;
        return $"{sign}{currency.Symbol} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }
}
