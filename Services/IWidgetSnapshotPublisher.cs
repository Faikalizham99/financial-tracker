using FinancialTracker.Models;

namespace FinancialTracker.Services;

public interface IWidgetSnapshotPublisher
{
    bool TryPublish(WidgetSnapshot snapshot);
}
