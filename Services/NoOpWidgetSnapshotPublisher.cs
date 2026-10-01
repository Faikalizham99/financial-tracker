using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class NoOpWidgetSnapshotPublisher : IWidgetSnapshotPublisher
{
    public bool TryPublish(WidgetSnapshot snapshot) => true;
}
