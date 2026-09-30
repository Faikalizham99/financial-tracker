using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed class ExpensesViewModel
{
    public ExpensesPresentation BuildPresentation(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption currency,
        DateTime displayedMonth,
        TransactionOption? paymentFilter,
        TransactionOption? categoryFilter,
        DateTime? startDate,
        DateTime? endDate,
        IReadOnlySet<int> expandedDescriptionIds,
        IReadOnlySet<DateTime> collapsedGroupDates,
        bool canModifyTransactions)
    {
        var hasDateRange = startDate is not null && endDate is not null;
        var filteredRecords = new List<TransactionRecord>();

        foreach (var record in records)
        {
            if (!hasDateRange &&
                (record.TransactionDate.Year != displayedMonth.Year ||
                 record.TransactionDate.Month != displayedMonth.Month))
            {
                continue;
            }

            if (paymentFilter is not null &&
                !record.PaymentMethod.Equals(
                    paymentFilter.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (categoryFilter is not null &&
                !record.Category.Equals(
                    categoryFilter.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var transactionDate = record.TransactionDate.Date;
            if (startDate is not null && transactionDate < startDate.Value.Date)
            {
                continue;
            }

            if (endDate is not null && transactionDate > endDate.Value.Date)
            {
                continue;
            }

            filteredRecords.Add(record);
        }

        filteredRecords.Sort(static (left, right) =>
        {
            var dateComparison = right.TransactionDate.CompareTo(left.TransactionDate);
            return dateComparison != 0
                ? dateComparison
                : right.Id.CompareTo(left.Id);
        });

        var groups = TransactionActivityGroupBuilder.Build(
            filteredRecords,
            currency.Symbol,
            expandedDescriptionIds,
            collapsedGroupDates,
            canModifyTransactions,
            DateTime.Today);
        var hasFilters = paymentFilter is not null ||
            categoryFilter is not null ||
            startDate is not null;
        var emptyTitle = hasFilters
            ? "No transactions match these filters"
            : $"No transactions in {displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture)}";
        var periodLabel = hasDateRange
            ? CompactDateRangeFormatter.Format(startDate!.Value, endDate!.Value)
            : displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        var hasSummaryScopeFilters = paymentFilter is not null || categoryFilter is not null;

        return new ExpensesPresentation(
            groups,
            filteredRecords,
            emptyTitle,
            periodLabel,
            hasDateRange,
            hasSummaryScopeFilters);
    }

}

public sealed record ExpensesPresentation(
    IReadOnlyList<TransactionActivityGroup> Groups,
    IReadOnlyList<TransactionRecord> FilteredRecords,
    string EmptyTitle,
    string PeriodLabel,
    bool HasDateRange,
    bool HasSummaryScopeFilters);
