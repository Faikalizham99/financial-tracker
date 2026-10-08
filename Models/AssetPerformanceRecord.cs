using SQLite;

namespace FinancialTracker.Models;

[Table("AssetPerformanceRecords")]
public sealed class AssetPerformanceRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    [NotNull]
    public string AssetKey { get; set; } = string.Empty;

    [Indexed]
    public DateTime EntryDate { get; set; }

    public long InvestedMinor { get; set; }

    public long ProfitLossMinor { get; set; }

    [NotNull]
    public string CurrencyCode { get; set; } = "MYR";

    [NotNull]
    public string Note { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
