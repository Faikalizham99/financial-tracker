using System.Globalization;
using FinancialTracker.Data;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class TransactionDataStore(LocalDatabase database)
{
    private const int SearchScanBatchSize = 160;
    private const int MaximumCachedMonths = 5;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly object monthCacheLock = new();
    private readonly Dictionary<int, IReadOnlyList<TransactionRecord>> monthCache = [];
    private readonly Dictionary<int, MonthLoad> monthLoads = [];
    private readonly LinkedList<int> monthCacheOrder = [];
    private IReadOnlyDictionary<int, TransactionRecord> recordsById =
        new Dictionary<int, TransactionRecord>();
    private IReadOnlyList<TransactionRecord> periodRecords = [];
    private int monthCacheGeneration;

    public IReadOnlyList<TransactionRecord> DashboardRecords { get; private set; } = [];
    public IReadOnlyList<TransactionRecord> DescriptionHistoryRecords { get; private set; } = [];
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
        var records = TryGetWholeMonth(
                startDateInclusive,
                endDateExclusive,
                out var month)
            ? await GetMonthAsync(month).ConfigureAwait(false)
            : await database.GetTransactionsAsync(
                    startDateInclusive,
                    endDateExclusive)
                .ConfigureAwait(false);
        periodRecords = records;
        RebuildRecordLookup();
        return records;
    }

    public async Task PrefetchMonthAsync(
        DateTime month,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        await GetMonthAsync(normalizedMonth).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
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
            var cacheGeneration = InvalidateMonthCache();
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
            CacheMonth(
                new DateTime(month.Year, month.Month, 1),
                snapshot.PeriodRecords,
                cacheGeneration);
            IsLoaded = true;
            return snapshot;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private Task<IReadOnlyList<TransactionRecord>> GetMonthAsync(DateTime month)
    {
        var monthKey = ToMonthKey(month);
        TaskCompletionSource<IReadOnlyList<TransactionRecord>> completion;
        int generation;
        lock (monthCacheLock)
        {
            if (monthCache.TryGetValue(monthKey, out var cachedRecords))
            {
                TouchCachedMonth(monthKey);
                return Task.FromResult(cachedRecords);
            }

            generation = monthCacheGeneration;
            if (monthLoads.TryGetValue(monthKey, out var existingLoad) &&
                existingLoad.Generation == generation)
            {
                return existingLoad.Task;
            }

            completion = new TaskCompletionSource<IReadOnlyList<TransactionRecord>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            monthLoads[monthKey] = new MonthLoad(generation, completion.Task);
        }

        _ = LoadAndCacheMonthAsync(
            month,
            monthKey,
            generation,
            completion);
        return completion.Task;
    }

    private async Task LoadAndCacheMonthAsync(
        DateTime month,
        int monthKey,
        int generation,
        TaskCompletionSource<IReadOnlyList<TransactionRecord>> completion)
    {
        try
        {
            var records = await database.GetTransactionsAsync(
                    month,
                    month.AddMonths(1))
                .ConfigureAwait(false);
            CacheMonth(month, records, generation);
            completion.TrySetResult(records);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            lock (monthCacheLock)
            {
                if (monthLoads.TryGetValue(monthKey, out var activeLoad) &&
                    activeLoad.Generation == generation)
                {
                    monthLoads.Remove(monthKey);
                }
            }
        }
    }

    private int InvalidateMonthCache()
    {
        lock (monthCacheLock)
        {
            monthCache.Clear();
            monthCacheOrder.Clear();
            return ++monthCacheGeneration;
        }
    }

    private void CacheMonth(
        DateTime month,
        IReadOnlyList<TransactionRecord> records,
        int generation)
    {
        var monthKey = ToMonthKey(month);
        lock (monthCacheLock)
        {
            if (generation != monthCacheGeneration)
            {
                return;
            }

            monthCache[monthKey] = records;
            TouchCachedMonth(monthKey);
            while (monthCacheOrder.Count > MaximumCachedMonths)
            {
                var oldestMonthKey = monthCacheOrder.First!.Value;
                monthCacheOrder.RemoveFirst();
                monthCache.Remove(oldestMonthKey);
            }
        }
    }

    private void TouchCachedMonth(int monthKey)
    {
        monthCacheOrder.Remove(monthKey);
        monthCacheOrder.AddLast(monthKey);
    }

    private static bool TryGetWholeMonth(
        DateTime startDateInclusive,
        DateTime endDateExclusive,
        out DateTime month)
    {
        month = new DateTime(
            startDateInclusive.Year,
            startDateInclusive.Month,
            1);
        return startDateInclusive == month &&
            endDateExclusive == month.AddMonths(1);
    }

    private static int ToMonthKey(DateTime month) =>
        (month.Year * 100) + month.Month;

    private void RebuildRecordLookup()
    {
        var lookup = new Dictionary<int, TransactionRecord>(
            DashboardRecords.Count +
            periodRecords.Count +
            DescriptionHistoryRecords.Count);
        AddRecords(lookup, DashboardRecords);
        AddRecords(lookup, periodRecords);
        AddRecords(lookup, DescriptionHistoryRecords);
        recordsById = lookup;
    }

    private static void AddRecords(
        Dictionary<int, TransactionRecord> destination,
        IEnumerable<TransactionRecord> source)
    {
        foreach (var record in source)
        {
            destination.TryAdd(record.Id, record);
        }
    }

    private static string BuildSearchText(TransactionRecord transaction)
    {
        var longDate = transaction.TransactionDate.ToString(
            "dddd d MMMM yyyy",
            CultureInfo.CurrentCulture);
        var shortDate = transaction.TransactionDate.ToString(
            "d MMM yyyy",
            CultureInfo.CurrentCulture);
        var amount = (transaction.AmountMinor / 100m).ToString(
            "N2",
            CultureInfo.InvariantCulture);
        return $"{transaction.Description} {transaction.Category} " +
            $"{transaction.PaymentMethod} {transaction.Type} {longDate} {shortDate} {amount}";
    }

    private sealed record MonthLoad(
        int Generation,
        Task<IReadOnlyList<TransactionRecord>> Task);
}
