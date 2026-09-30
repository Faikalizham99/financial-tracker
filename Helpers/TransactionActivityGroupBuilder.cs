using System.Globalization;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.Helpers;

public static class TransactionActivityGroupBuilder
{
    public static IReadOnlyList<TransactionActivityGroup> Build(
        IReadOnlyList<TransactionRecord> records,
        string currencySymbol,
        IReadOnlySet<int> expandedDescriptionIds,
        IReadOnlySet<DateTime> collapsedGroupDates,
        bool canModifyTransactions,
        DateTime today)
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
                GetDateGroupTitle(date, today),
                items,
                netAmountMinor,
                currencySymbol,
                !collapsedGroupDates.Contains(date)));
        }

        return groups;
    }

    private static string GetDateGroupTitle(DateTime date, DateTime today)
    {
        var dayLabel = date.Date switch
        {
            var value when value == today => "TODAY",
            var value when value == today.AddDays(-1) => "YESTERDAY",
            _ => date.ToString("dddd", CultureInfo.CurrentCulture).ToUpperInvariant()
        };

        var dateLabel = date.ToString("d MMM yyyy", CultureInfo.CurrentCulture)
            .ToUpperInvariant();
        return $"{dayLabel} \u00B7 {dateLabel}";
    }
}
