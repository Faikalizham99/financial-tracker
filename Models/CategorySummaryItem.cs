using FinancialTracker.Helpers;

namespace FinancialTracker.Models;

public sealed class CategorySummaryItem
{
    public CategorySummaryItem(
        TransactionOption option,
        long amountMinor,
        long totalMinor,
        string currencySymbol,
        bool isIncome,
        bool showDivider)
    {
        Title = option.Title;
        IconAsset = option.IconAsset;
        AmountText = MoneyFormatter.FormatMinor(amountMinor, currencySymbol);
        Share = totalMinor > 0 ? (double)amountMinor / totalMinor : 0;
        PercentageText = Share.ToString("P0");
        IsIncome = isIncome;
        ShowDivider = showDivider;
    }

    public string Title { get; }
    public string IconAsset { get; }
    public string AmountText { get; }
    public double Share { get; }
    public string PercentageText { get; }
    public bool IsIncome { get; }
    public bool ShowDivider { get; }
}
