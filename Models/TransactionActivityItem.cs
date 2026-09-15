using System.Globalization;
using FinancialTracker.Services;

namespace FinancialTracker.Models;

public sealed class TransactionActivityItem
{
    private TransactionActivityItem()
    {
    }

    public int Id { get; private init; }
    public string Description { get; private init; } = string.Empty;
    public string DetailText { get; private init; } = string.Empty;
    public string DashboardDetailText { get; private init; } = string.Empty;
    public string DashboardDateText { get; private init; } = string.Empty;
    public string AmountText { get; private init; } = string.Empty;
    public string IconAsset { get; private init; } = string.Empty;
    public string PaymentMethodIconAsset { get; private init; } = string.Empty;
    public bool IsIncome { get; private init; }
    public bool ShowDivider { get; private init; }
    public DateTime TransactionDate { get; private init; }

    public static TransactionActivityItem FromRecord(
        TransactionRecord record,
        bool showDivider = false)
    {
        var isIncome = record.Type.Equals("Income", StringComparison.OrdinalIgnoreCase);
        var categories = isIncome
            ? TransactionCatalog.IncomeCategories
            : TransactionCatalog.ExpenseCategories;
        var category = categories.FirstOrDefault(item =>
                item.Key.Equals(record.Category, StringComparison.OrdinalIgnoreCase))
            ?? categories[^1];
        var paymentMethod = TransactionCatalog.PaymentMethods.FirstOrDefault(item =>
                item.Key.Equals(record.PaymentMethod, StringComparison.OrdinalIgnoreCase))
            ?? TransactionCatalog.PaymentMethods[^1];
        var symbol = record.CurrencyCode.ToUpperInvariant() switch
        {
            "USD" => "$",
            "SGD" => "S$",
            "KRW" => "₩",
            _ => "RM"
        };
        var dateText = record.TransactionDate.Date switch
        {
            var date when date == DateTime.Today => "Today",
            var date when date == DateTime.Today.AddDays(-1) => "Yesterday",
            var date => date.ToString("d MMM yyyy", CultureInfo.CurrentCulture)
        };

        return new TransactionActivityItem
        {
            Id = record.Id,
            Description = record.Description,
            DetailText = $"{record.Category} · {record.PaymentMethod}",
            DashboardDetailText = $"{record.Category} · {record.PaymentMethod}",
            DashboardDateText = dateText,
            AmountText = $"{(isIncome ? "+" : "−")} {symbol} {(record.AmountMinor / 100m).ToString("N2", CultureInfo.InvariantCulture)}",
            IconAsset = category.IconAsset,
            PaymentMethodIconAsset = paymentMethod.IconAsset,
            IsIncome = isIncome,
            ShowDivider = showDivider,
            TransactionDate = record.TransactionDate.Date
        };
    }
}

public sealed class TransactionActivityGroup(
    string title,
    IReadOnlyList<TransactionActivityItem> items)
{
    public string Title { get; } = title;
    public IReadOnlyList<TransactionActivityItem> Items { get; } = items;
}
