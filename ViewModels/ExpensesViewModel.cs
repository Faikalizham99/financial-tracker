using System.Globalization;
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

        var groups = BuildGroups(
            filteredRecords,
            currency.Symbol,
            expandedDescriptionIds,
            collapsedGroupDates,
            canModifyTransactions);
        var hasFilters = paymentFilter is not null ||
            categoryFilter is not null ||
            startDate is not null;
        var emptyTitle = hasFilters
            ? "No transactions match these filters"
            : $"No transactions in {displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture)}";
        var periodLabel = hasDateRange
            ? FormatDateRange(startDate!.Value, endDate!.Value)
            : displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

        return new ExpensesPresentation(
            groups,
            emptyTitle,
            periodLabel,
            hasDateRange);
    }

    private static IReadOnlyList<TransactionActivityGroup> BuildGroups(
        IReadOnlyList<TransactionRecord> records,
        string currencySymbol,
        IReadOnlySet<int> expandedDescriptionIds,
        IReadOnlySet<DateTime> collapsedGroupDates,
        bool canModifyTransactions)
    {
        var groups = new List<TransactionActivityGroup>();
        var index = 0;

        while (index < records.Count)
        {
            var date = records[index].TransactionDate.Date;
            var groupRecords = new List<TransactionRecord>();
            long netAmountMinor = 0;

            while (index < records.Count && records[index].TransactionDate.Date == date)
            {
                var record = records[index++];
                groupRecords.Add(record);
                netAmountMinor += TransactionCatalog.IsIncomeType(record.Type)
                    ? record.AmountMinor
                    : -record.AmountMinor;
            }

            var items = new List<TransactionActivityItem>(groupRecords.Count);
            for (var itemIndex = 0; itemIndex < groupRecords.Count; itemIndex++)
            {
                var record = groupRecords[itemIndex];
                items.Add(TransactionActivityItem.FromRecord(
                    record,
                    currencySymbol,
                    itemIndex < groupRecords.Count - 1,
                    expandedDescriptionIds.Contains(record.Id),
                    canModifyTransactions));
            }

            groups.Add(new TransactionActivityGroup(
                date,
                GetDateGroupTitle(date),
                items,
                netAmountMinor,
                currencySymbol,
                !collapsedGroupDates.Contains(date)));
        }

        return groups;
    }

    private static string FormatDateRange(DateTime startDate, DateTime endDate)
    {
        if (startDate == endDate)
        {
            return startDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }

        if (startDate.Year == endDate.Year && startDate.Month == endDate.Month)
        {
            return $"{startDate.Day}–{endDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";
        }

        if (startDate.Year == endDate.Year)
        {
            return $"{startDate.ToString("d MMM", CultureInfo.CurrentCulture)} – {endDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";
        }

        return $"{startDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)} – {endDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";
    }

    private static string GetDateGroupTitle(DateTime date)
    {
        var dayLabel = date.Date switch
        {
            var value when value == DateTime.Today => "TODAY",
            var value when value == DateTime.Today.AddDays(-1) => "YESTERDAY",
            _ => date.ToString("dddd", CultureInfo.CurrentCulture).ToUpperInvariant()
        };

        var dateLabel = date.ToString("d MMM yyyy", CultureInfo.CurrentCulture)
            .ToUpperInvariant();
        return $"{dayLabel} · {dateLabel}";
    }
}

public sealed record ExpensesPresentation(
    IReadOnlyList<TransactionActivityGroup> Groups,
    string EmptyTitle,
    string PeriodLabel,
    bool HasDateRange);
