using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class WidgetSnapshotBuilder
{
    private const int SchemaVersion = 1;

    public static WidgetSnapshot Build(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption currency,
        bool includeInvestment,
        DateTime month,
        DateTimeOffset updatedAt)
    {
        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        long incomeMinor = 0;
        long expenseMinor = 0;
        var transactionCount = 0;

        foreach (var record in records)
        {
            var transactionDate = record.TransactionDate.Date;
            if (transactionDate.Year != normalizedMonth.Year ||
                transactionDate.Month != normalizedMonth.Month ||
                (!includeInvestment &&
                    record.Category.Equals(
                        TransactionCatalog.InvestmentCategoryKey,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (TransactionCatalog.IsIncomeType(record.Type))
            {
                incomeMinor += record.AmountMinor;
                transactionCount++;
            }
            else if (TransactionCatalog.IsExpenseType(record.Type))
            {
                expenseMinor += record.AmountMinor;
                transactionCount++;
            }
        }

        return new WidgetSnapshot(
            SchemaVersion,
            normalizedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            MoneyFormatter.FormatMinor(
                incomeMinor - expenseMinor,
                currency.Symbol,
                separateSign: true),
            MoneyFormatter.FormatMinor(incomeMinor, currency.Symbol),
            MoneyFormatter.FormatMinor(expenseMinor, currency.Symbol),
            transactionCount,
            updatedAt.ToUnixTimeSeconds());
    }
}
