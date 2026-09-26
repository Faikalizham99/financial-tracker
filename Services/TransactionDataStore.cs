using System.Globalization;
using FinancialTracker.Data;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class TransactionDataStore(LocalDatabase database)
{
    private const int SearchScanBatchSize = 160;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private IReadOnlyDictionary<int, TransactionRecord> recordsById =
        new Dictionary<int, TransactionRecord>();
    private IReadOnlyList<TransactionRecord> periodRecords = [];

    public IReadOnlyList<TransactionRecord> DashboardRecords { get; private set; } = [];
    public IReadOnlyList<TransactionRecord> DescriptionHistoryRecords { get; private set; } = [];
    public IReadOnlyList<TransactionRecord> PeriodRecords => periodRecords;
    public bool IsLoaded { get; private set; }

    public Task<TransactionDataSnapshot> LoadStartupAsync(
        DateTime month,
        CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(month, useStartupRecovery: true, cancellationToken);

    public Task<TransactionDataSnapshot> RefreshAsync(
        DateTime month,
        bool useStartupRecovery = false,
        CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(month, useStartupRecovery, cancellationToken);

    public async Task<IReadOnlyList<TransactionRecord>> GetPeriodAsync(
        DateTime startDateInclusive,
        DateTime endDateExclusive)
    {
        var records = await database.GetTransactionsAsync(
            startDateInclusive,
            endDateExclusive).ConfigureAwait(false);
        periodRecords = records;
        RebuildRecordLookup();
        return records;
    }

    public async Task<TransactionSearchPage> SearchAsync(
        string query,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var normalizedLimit = Math.Max(1, limit);
        var nextOffset = Math.Max(0, offset);
        var matches = new List<TransactionRecord>(normalizedLimit);

        while (matches.Count < normalizedLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await database.GetTransactionPageAsync(
                nextOffset,
                SearchScanBatchSize).ConfigureAwait(false);
            if (page.Count == 0)
            {
                return new TransactionSearchPage(matches, nextOffset, HasMore: false);
            }

            for (var index = 0; index < page.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                nextOffset++;
                var transaction = page[index];
                if (!BuildSearchText(transaction).Contains(
                        query,
                        StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                matches.Add(transaction);
                if (matches.Count == normalizedLimit)
                {
                    var hasUnscannedRecords =
                        index < page.Count - 1 || page.Count == SearchScanBatchSize;
                    return new TransactionSearchPage(
                        matches,
                        nextOffset,
                        hasUnscannedRecords);
                }
            }

            if (page.Count < SearchScanBatchSize)
            {
                return new TransactionSearchPage(matches, nextOffset, HasMore: false);
            }
        }

        return new TransactionSearchPage(matches, nextOffset, HasMore: true);
    }

    public TransactionRecord? FindCached(int transactionId) =>
        recordsById.GetValueOrDefault(transactionId);

    public Task<TransactionRecord?> FindStoredAsync(int transactionId) =>
        database.GetTransactionAsync(transactionId);

    private async Task<TransactionDataSnapshot> RefreshCoreAsync(
        DateTime month,
        bool useStartupRecovery,
        CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = useStartupRecovery
                ? await database.GetTransactionSnapshotForStartupAsync(
                    month,
                    cancellationToken).ConfigureAwait(false)
                : await database.GetTransactionSnapshotAsync(month)
                    .ConfigureAwait(false);
            DashboardRecords = snapshot.DashboardRecords;
            periodRecords = snapshot.PeriodRecords;
            DescriptionHistoryRecords = snapshot.DescriptionHistoryRecords;
            RebuildRecordLookup();
            IsLoaded = true;
            return snapshot;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private void RebuildRecordLookup()
    {
        recordsById = DashboardRecords
            .Concat(periodRecords)
            .Concat(DescriptionHistoryRecords)
            .DistinctBy(record => record.Id)
            .ToDictionary(record => record.Id);
    }

    private static string BuildSearchText(TransactionRecord transaction) =>
        string.Join(
            ' ',
            transaction.Description,
            transaction.Category,
            transaction.PaymentMethod,
            transaction.Type,
            transaction.TransactionDate.ToString(
                "dddd d MMMM yyyy",
                CultureInfo.CurrentCulture),
            transaction.TransactionDate.ToString(
                "d MMM yyyy",
                CultureInfo.CurrentCulture),
            (transaction.AmountMinor / 100m).ToString(
                "N2",
                CultureInfo.InvariantCulture));
}
