using FinancialTracker.Models;

namespace FinancialTracker.Services;

public interface IAssetWidgetSnapshotPublisher
{
    bool IsSupported { get; }

    bool TryPublish(AssetWidgetSnapshot snapshot);
}
