using FinancialTracker.Data;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class TransactionDataStore(LocalDatabase database)
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private IReadOnlyDictionary<int, TransactionRecord> recordsById =
        new Dictionary<int, TransactionRecord>();

    public IReadOnlyList<TransactionRecord> Records { get; private set; } = [];
    public bool IsLoaded { get; private set; }

    public Task<IReadOnlyList<TransactionRecord>> LoadStartupAsync(
        CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(useStartupRecovery: true, cancellationToken);

    public Task<IReadOnlyList<TransactionRecord>> RefreshAsync(
        bool useStartupRecovery = false,
        CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(useStartupRecovery, cancellationToken);

    public TransactionRecord? FindCached(int transactionId) =>
        recordsById.GetValueOrDefault(transactionId);

    public Task<TransactionRecord?> FindStoredAsync(int transactionId) =>
        database.GetTransactionAsync(transactionId);

    private async Task<IReadOnlyList<TransactionRecord>> RefreshCoreAsync(
        bool useStartupRecovery,
        CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            var records = useStartupRecovery
                ? await database.GetTransactionsForStartupAsync(cancellationToken)
                : await database.GetTransactionsAsync();
            Records = records;
            recordsById = records.ToDictionary(record => record.Id);
            IsLoaded = true;
            return records;
        }
        finally
        {
            refreshLock.Release();
        }
    }
}
