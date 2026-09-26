namespace FinancialTracker.Models;

public sealed record AssetSnapshotData(
    AssetSnapshotRecord Snapshot,
    IReadOnlyList<AssetSnapshotValueRecord> Values);
