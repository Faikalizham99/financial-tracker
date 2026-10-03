using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class AssetWidgetSnapshotCoordinator(
    IAssetWidgetSnapshotPublisher publisher,
    LocalDatabase database)
{
    private readonly object synchronization = new();
    private AssetWidgetSnapshot? lastPublishedSnapshot;
    private int publishRequestVersion;

    public void QueuePublish(CurrencyOption currency, DateTime throughMonth)
    {
        if (!publisher.IsSupported)
        {
            return;
        }

        var requestVersion = Interlocked.Increment(ref publishRequestVersion);
        _ = PublishIfCurrentAsync(currency, throughMonth, requestVersion);
    }

    private async Task PublishIfCurrentAsync(
        CurrencyOption currency,
        DateTime throughMonth,
        int requestVersion)
    {
        await Task.Yield();
        try
        {
            var normalizedMonth = new DateTime(throughMonth.Year, throughMonth.Month, 1);
            var snapshots = await database.GetAssetSnapshotsThroughAsync(
                MonthKeyConverter.FromDate(normalizedMonth),
                AssetWidgetSnapshotBuilder.HistoryMonthCount + 1).ConfigureAwait(false);
            if (requestVersion != Volatile.Read(ref publishRequestVersion))
            {
                return;
            }

            var snapshot = AssetWidgetSnapshotBuilder.Build(
                snapshots,
                currency,
                normalizedMonth,
                DateTimeOffset.UtcNow);
            lock (synchronization)
            {
                if (requestVersion != publishRequestVersion ||
                    HasSameContent(lastPublishedSnapshot, snapshot))
                {
                    return;
                }

                if (publisher.TryPublish(snapshot))
                {
                    lastPublishedSnapshot = snapshot;
                }
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Asset widget snapshot publishing failed: {exception}");
        }
    }

    private static bool HasSameContent(
        AssetWidgetSnapshot? previous,
        AssetWidgetSnapshot current) =>
        previous is not null &&
        previous.Months.SequenceEqual(current.Months) &&
        previous with
        {
            Months = current.Months,
            UpdatedAtUnixSeconds = current.UpdatedAtUnixSeconds
        } == current;
}
