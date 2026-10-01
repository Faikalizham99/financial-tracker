using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class WidgetSnapshotCoordinator(
    IWidgetSnapshotPublisher publisher,
    LocalDatabase database)
{
    private readonly object synchronization = new();
    private WidgetSnapshot? lastPublishedSnapshot;
    private int publishRequestVersion;

    public void QueuePublish(
        CurrencyOption currency,
        bool includeInvestment,
        DateTime month)
    {
        if (!publisher.IsSupported)
        {
            return;
        }

        var requestVersion = Interlocked.Increment(ref publishRequestVersion);
        _ = PublishIfCurrentAsync(
            currency,
            includeInvestment,
            month,
            requestVersion);
    }

    private async Task PublishIfCurrentAsync(
        CurrencyOption currency,
        bool includeInvestment,
        DateTime month,
        int requestVersion)
    {
        await Task.Yield();
        if (requestVersion != Volatile.Read(ref publishRequestVersion))
        {
            return;
        }

        IReadOnlyList<TransactionRecord> records;
        IReadOnlyList<MonthlyBudgetRecord> budgets;
        try
        {
            var normalizedMonth = new DateTime(month.Year, month.Month, 1);
            var firstMonth = normalizedMonth.AddMonths(
                1 - WidgetSnapshotBuilder.HistoryMonthCount);
            records = await database.GetTransactionsAsync(
                firstMonth,
                normalizedMonth.AddMonths(1)).ConfigureAwait(false);
            if (requestVersion != Volatile.Read(ref publishRequestVersion))
            {
                return;
            }

            budgets = await database.GetMonthlyBudgetsAsync(
                MonthKeyConverter.FromDate(firstMonth),
                MonthKeyConverter.FromDate(normalizedMonth)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Widget history loading failed: {exception}");
            return;
        }

        if (requestVersion != Volatile.Read(ref publishRequestVersion))
        {
            return;
        }

        var snapshot = WidgetSnapshotBuilder.Build(
            records,
            budgets,
            currency,
            includeInvestment,
            month,
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
        previous.Months.SequenceEqual(current.Months) &&
        previous with
        {
            Months = current.Months,
            UpdatedAtUnixSeconds = current.UpdatedAtUnixSeconds
        } == current;
}
