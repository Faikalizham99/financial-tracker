using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class WidgetSnapshotCoordinator(
    IWidgetSnapshotPublisher publisher)
{
    private readonly object synchronization = new();
    private WidgetSnapshot? lastPublishedSnapshot;

    public void PublishIfChanged(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption currency,
        bool includeInvestment,
        DateTime month)
    {
        var snapshot = WidgetSnapshotBuilder.Build(
            records,
            currency,
            includeInvestment,
            month,
            DateTimeOffset.UtcNow);

        lock (synchronization)
        {
            if (HasSameContent(lastPublishedSnapshot, snapshot))
            {
                return;
            }

            if (publisher.TryPublish(snapshot))
            {
                lastPublishedSnapshot = snapshot;
            }
        }
    }

    private static bool HasSameContent(
        WidgetSnapshot? previous,
        WidgetSnapshot current) =>
        previous is not null &&
        previous == current with
        {
            UpdatedAtUnixSeconds = previous.UpdatedAtUnixSeconds
        };
}
