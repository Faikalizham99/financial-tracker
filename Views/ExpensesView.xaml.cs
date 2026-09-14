namespace FinancialTracker.Views;

using System.Globalization;
using FinancialTracker.Models;

public partial class ExpensesView : ContentView
{
    public ExpensesView()
    {
        InitializeComponent();
    }

    public void Refresh(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency)
    {
        var groups = records
            .GroupBy(record => record.TransactionDate.Date)
            .Select(group =>
            {
                var groupRecords = group.ToList();
                var items = groupRecords
                    .Select((record, index) => TransactionActivityItem.FromRecord(
                        record,
                        index < groupRecords.Count - 1))
                    .ToList();

                return new TransactionActivityGroup(
                    GetDateGroupTitle(group.Key),
                    items);
            })
            .ToList();

        BindableLayout.SetItemsSource(ActivityGroupsLayout, groups);
        ActivityGroupsLayout.IsVisible = groups.Count > 0;
        EmptyActivityState.IsVisible = groups.Count == 0;

        var today = DateTime.Today;
        var monthlyExpenses = records
            .Where(record =>
                record.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase) &&
                record.CurrencyCode.Equals(selectedCurrency.Code, StringComparison.OrdinalIgnoreCase) &&
                record.TransactionDate.Year == today.Year &&
                record.TransactionDate.Month == today.Month)
            .ToList();
        var totalMinor = monthlyExpenses.Sum(record => record.AmountMinor);

        MonthLabel.Text = today.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        TotalSpendingLabel.Text =
            $"{selectedCurrency.Symbol} {(totalMinor / 100m).ToString("N2", CultureInfo.InvariantCulture)}";
        TransactionCountLabel.Text = monthlyExpenses.Count.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetDateGroupTitle(DateTime date) =>
        date.Date switch
        {
            var value when value == DateTime.Today => "TODAY",
            var value when value == DateTime.Today.AddDays(-1) => "YESTERDAY",
            var value => value.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture).ToUpperInvariant()
        };
}
