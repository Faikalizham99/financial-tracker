using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Services;
using SQLite;

namespace FinancialTracker.Models;

[Table("PendingTransactions")]
public sealed class PendingTransactionRecord
{
    [PrimaryKey, AutoIncrement]
    public long Id { get; set; }

    [NotNull, Indexed(Unique = true)]
    public string CaptureKey { get; set; } = string.Empty;

    public string SourceApp { get; set; } = string.Empty;
    public string NotificationTitle { get; set; } = string.Empty;
    public string NotificationSubtitle { get; set; } = string.Empty;
    public string NotificationMessage { get; set; } = string.Empty;
    public long? AmountMinor { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public string SuggestedDescription { get; set; } = string.Empty;
    public string SuggestedType { get; set; } = TransactionCatalog.ExpenseTypeKey;
    public string SuggestedCategory { get; set; } = "Others";
    public string SuggestedPaymentMethod { get; set; } = "Others";
    public long ReceivedAtUnixMs { get; set; }
    public long CreatedAtUnixMs { get; set; }
    public string ParseStatus { get; set; } = "NeedsReview";

    [Ignore]
    public bool HasDetectedAmount => AmountMinor is > 0;

    [Ignore]
    public string SourceDisplay => string.IsNullOrWhiteSpace(SourceApp)
        ? "Notification"
        : SourceApp.Trim();

    [Ignore]
    public string DescriptionDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SuggestedDescription))
            {
                return SuggestedDescription.Trim();
            }

            if (!string.IsNullOrWhiteSpace(NotificationTitle))
            {
                return NotificationTitle.Trim();
            }

            return "Description needed";
        }
    }

    [Ignore]
    public string AmountDisplay => AmountMinor is > 0
        ? MoneyFormatter.FormatMinor(AmountMinor.Value, CurrencySymbol)
        : "Amount needed";

    [Ignore]
    public string AmountStatus => HasDetectedAmount ? "Amount detected" : "Check amount";

    [Ignore]
    public string CurrencySymbol => CurrencyCode.Trim().ToUpperInvariant() switch
    {
        "USD" => "$",
        "SGD" => "S$",
        "KRW" => "₩",
        _ => "RM"
    };

    [Ignore]
    public DateTime ReceivedAt => DateTimeOffset
        .FromUnixTimeMilliseconds(Math.Max(0, ReceivedAtUnixMs))
        .LocalDateTime;

    [Ignore]
    public string ReceivedAtDisplay => ReceivedAt.ToString(
        "d MMM yyyy · h:mm tt",
        CultureInfo.CurrentCulture);

    [Ignore]
    public string SuggestionDisplay
    {
        get
        {
            var isIncome = TransactionCatalog.IsIncomeType(SuggestedType);
            var category = TransactionCatalog.GetCategory(
                SuggestedCategory,
                isIncome).Title;
            var payment = TransactionCatalog.GetPaymentMethod(
                SuggestedPaymentMethod).Title;
            return $"{(isIncome ? "Income" : "Expense")} · {category} · {payment}";
        }
    }

    [Ignore]
    public string RawNotificationDisplay
    {
        get
        {
            var text = string.Join(
                Environment.NewLine,
                new[] { NotificationTitle, NotificationSubtitle, NotificationMessage }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()));
            return string.IsNullOrWhiteSpace(text)
                ? "No notification text was supplied."
                : text;
        }
    }

    public TransactionRecord CreateTransaction(string fallbackCurrencyCode) => new()
    {
        Type = TransactionCatalog.GetTransactionType(SuggestedType).Key,
        Category = TransactionCatalog.GetCategory(
            SuggestedCategory,
            TransactionCatalog.IsIncomeType(SuggestedType)).Key,
        PaymentMethod = TransactionCatalog.GetPaymentMethod(SuggestedPaymentMethod).Key,
        Description = DescriptionDisplay,
        AmountMinor = Math.Max(0, AmountMinor ?? 0),
        CurrencyCode = string.IsNullOrWhiteSpace(CurrencyCode)
            ? fallbackCurrencyCode
            : CurrencyCode.Trim().ToUpperInvariant(),
        TransactionDate = ReceivedAt.Date,
        CreatedAtUtc = DateTime.UtcNow,
        ExternalImportKey = CaptureKey
    };
}
