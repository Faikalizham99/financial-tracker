using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed partial class TransactionStatisticsViewModel
{
    private void RebuildBreakdown()
    {
        if (data is null)
        {
            BreakdownItems = [];
            return;
        }

        var isIncome = UsesIncomeBreakdown;
        var currentAmounts = BuildBreakdownAmounts(data.SourceMonthRecords, isIncome);
        var previousAmounts = BuildBreakdownAmounts(data.PreviousMonthRecords, isIncome);
        var totalMinor = currentAmounts.Values.Sum();
        var dayCount = DateTime.DaysInMonth(data.SourceMonth.Year, data.SourceMonth.Month);

        var items = currentAmounts
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => ResolveOption(pair.Key, isIncome).Title)
            .Select(pair => CreateBreakdownItem(
                pair.Key,
                pair.Value,
                previousAmounts.GetValueOrDefault(pair.Key),
                totalMinor,
                dayCount,
                isIncome))
            .ToList();

        for (var index = 0; index < items.Count; index++)
        {
            items[index].ShowDivider = index < items.Count - 1;
        }

        if (SelectedBreakdownKey is not null &&
            items.All(item => !item.Key.Equals(
                SelectedBreakdownKey,
                StringComparison.OrdinalIgnoreCase)))
        {
            SelectedBreakdownKey = null;
        }

        BreakdownItems = items;
    }

    private TransactionStatisticsBreakdownItem CreateBreakdownItem(
        string key,
        long amountMinor,
        long previousMinor,
        long totalMinor,
        int dayCount,
        bool isIncome)
    {
        var option = ResolveOption(key, isIncome);
        var difference = amountMinor - previousMinor;
        return new TransactionStatisticsBreakdownItem
        {
            Key = key,
            Title = option.Title,
            IconAsset = option.IconAsset,
            AmountText = FormatMoney(amountMinor),
            DetailText = $"{FormatPercentage(amountMinor, totalMinor)} \u00B7 " +
                $"{FormatMoney(AveragePerDay(amountMinor, dayCount))} daily",
            ChangeText = FormatBreakdownChange(difference, previousMinor),
            AmountMinor = amountMinor,
            Share = totalMinor > 0 ? (double)amountMinor / totalMinor : 0,
            ChartColor = TransactionChartPalette.GetColor(
                key,
                SelectedBreakdownDimension),
            IsFavorable = difference != 0 && (isIncome ? difference > 0 : difference < 0),
            IsUnfavorable = difference != 0 && (isIncome ? difference < 0 : difference > 0),
            IsSelected = key.Equals(
                SelectedBreakdownKey,
                StringComparison.OrdinalIgnoreCase)
        };
    }

    public TransactionStatisticsDetailRequest? CreateDetailRequest(
        TransactionStatisticsBreakdownItem item)
    {
        if (data is null)
        {
            return null;
        }

        return new TransactionStatisticsDetailRequest(
            data.SourceMonth,
            data.CurrencySymbol,
            SelectedBreakdownDimension,
            item.Key,
            item.Title,
            item.IconAsset,
            UsesIncomeBreakdown,
            data.TimelineRecords);
    }

    private Dictionary<string, long> BuildBreakdownAmounts(
        IReadOnlyList<TransactionRecord> records,
        bool isIncome)
    {
        var amounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (TransactionCatalog.IsIncomeType(record.Type) != isIncome)
            {
                continue;
            }

            var option = SelectedBreakdownDimension ==
                TransactionStatisticsBreakdownDimension.Category
                    ? TransactionCatalog.GetCategory(record.Category, isIncome)
                    : TransactionCatalog.GetPaymentMethod(record.PaymentMethod);
            amounts[option.Key] = amounts.GetValueOrDefault(option.Key) + record.AmountMinor;
        }

        return amounts;
    }

    private TransactionOption ResolveOption(string key, bool isIncome) =>
        SelectedBreakdownDimension == TransactionStatisticsBreakdownDimension.Category
            ? TransactionCatalog.GetCategory(key, isIncome)
            : TransactionCatalog.GetPaymentMethod(key);
}
