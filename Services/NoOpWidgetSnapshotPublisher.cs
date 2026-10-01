using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class NoOpWidgetSnapshotPublisher : IWidgetSnapshotPublisher
{
    public bool IsSupported => false;

    public bool TryPublish(WidgetSnapshot snapshot) => true;
}
