using SQLite;

namespace FinancialTracker.Models;

[Table("AssetSnapshotValues")]
public sealed class AssetSnapshotValueRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SnapshotId { get; set; }

    [NotNull]
    public string AssetKey { get; set; } = string.Empty;

    public long AmountMinor { get; set; }
}
