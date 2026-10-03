using FinancialTracker.Models;

namespace FinancialTracker.Services;

public interface IWidgetSnapshotPublisher
{
    bool IsSupported { get; }

    bool TryPublish(WidgetSnapshot snapshot);
}
