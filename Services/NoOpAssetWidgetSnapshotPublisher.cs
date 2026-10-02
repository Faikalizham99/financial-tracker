using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class NoOpAssetWidgetSnapshotPublisher : IAssetWidgetSnapshotPublisher
{
    public bool IsSupported => false;

    public bool TryPublish(AssetWidgetSnapshot snapshot) => true;
}
