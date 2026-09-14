using SQLite;

namespace FinancialTracker.Models;

[Table("Transactions")]
public sealed class TransactionRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Type { get; set; } = "Expense";

    [NotNull]
    public string Category { get; set; } = "Others";

    [NotNull]
    public string PaymentMethod { get; set; } = "Cash";

    public string Description { get; set; } = string.Empty;

    public long AmountMinor { get; set; }

    [NotNull]
    public string CurrencyCode { get; set; } = "MYR";

    public DateTime TransactionDate { get; set; } = DateTime.Today;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
