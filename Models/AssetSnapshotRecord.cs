using SQLite;

namespace FinancialTracker.Models;

[Table("AssetSnapshots")]
public sealed class AssetSnapshotRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public int MonthKey { get; set; }

    public DateTime EntryDate { get; set; }

    [NotNull]
    public string CurrencyCode { get; set; } = "MYR";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
