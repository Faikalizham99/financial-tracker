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
        long incomeMinor = 0;
        long expenseMinor = 0;
        var transactionCount = 0;

        foreach (var record in records)
        {
            if (!record.CurrencyCode.Equals(selectedCurrency.Code, StringComparison.OrdinalIgnoreCase) ||
                record.TransactionDate.Year != month.Year ||
                record.TransactionDate.Month != month.Month)
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

        SummaryMonthLabel.Text = month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        SummaryAvailableCaptionLabel.Text =
            $"Available for {month.ToString("MMMM", CultureInfo.CurrentCulture)}";
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
