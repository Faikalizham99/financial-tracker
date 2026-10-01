using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class WidgetSnapshotCoordinator(
    IWidgetSnapshotPublisher publisher,
    MonthlyBudgetService monthlyBudgetService)
{
    private readonly object synchronization = new();
    private WidgetSnapshot? lastPublishedSnapshot;
    private int publishRequestVersion;

    public void QueuePublish(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption currency,
        bool includeInvestment,
        DateTime month)
    {
        var requestVersion = Interlocked.Increment(ref publishRequestVersion);
        _ = PublishIfCurrentAsync(
            records.ToArray(),
            currency,
            includeInvestment,
            month,
            requestVersion);
    }

    private async Task PublishIfCurrentAsync(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption currency,
        bool includeInvestment,
        DateTime month,
        int requestVersion)
    {
        MonthlyBudgetRecord? budget;
        try
        {
            budget = await monthlyBudgetService.GetAsync(month).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Widget budget loading failed: {exception}");
            budget = null;
        }

        if (requestVersion != Volatile.Read(ref publishRequestVersion))
        {
            return;
        }

        var snapshot = WidgetSnapshotBuilder.Build(
            records,
            currency,
            includeInvestment,
            month,
            budget,
            DateTimeOffset.UtcNow);

        lock (synchronization)
        {
            if (requestVersion != publishRequestVersion)
            {
                return;
            }

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
