using FinancialTracker.Models;
using SQLite;

#if IOS
using Foundation;
#endif

namespace FinancialTracker.Services;

public sealed class PendingTransactionInboxService
{
    private const string DatabaseFileName = "pending-transactions.db3";
    private readonly SemaphoreSlim gate = new(1, 1);
    private SQLiteAsyncConnection? connection;
    private bool initialized;

    public async Task<IReadOnlyList<PendingTransactionRecord>> GetPendingAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            return await Connection.QueryAsync<PendingTransactionRecord>(
                    "SELECT * FROM PendingTransactions " +
                    "ORDER BY ReceivedAtUnixMs DESC, Id DESC")
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> GetPendingCountAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            return await Connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM PendingTransactions")
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DeleteAsync(long id)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            await Connection.ExecuteAsync(
                    "DELETE FROM PendingTransactions WHERE Id = ?",
                    id)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private SQLiteAsyncConnection Connection => connection ??=
        new SQLiteAsyncConnection(
            ResolveDatabasePath(),
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.FullMutex);

    private async Task EnsureInitializedAsync()
    {
        if (initialized)
        {
            return;
        }

        await Connection.ExecuteAsync("PRAGMA busy_timeout = 5000")
            .ConfigureAwait(false);
        await Connection.ExecuteAsync("PRAGMA journal_mode = WAL")
            .ConfigureAwait(false);
        await Connection.ExecuteAsync("PRAGMA synchronous = FULL")
            .ConfigureAwait(false);
        await Connection.CreateTableAsync<PendingTransactionRecord>()
            .ConfigureAwait(false);
        await Connection.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS " +
                "IX_PendingTransactions_CaptureKey " +
                "ON PendingTransactions (CaptureKey)")
            .ConfigureAwait(false);
        initialized = true;
    }

    private static string ResolveDatabasePath()
    {
#if IOS
        foreach (var identifier in WidgetConstants.AppGroupIdentifiers)
        {
            var container = NSFileManager.DefaultManager.GetContainerUrl(identifier);
            if (container?.Path is not string containerPath)
            {
                continue;
            }

            var directory = Path.Combine(containerPath, "Library", "FinancialTracker");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, DatabaseFileName);
        }

        throw new InvalidOperationException(
            "The shared pending transaction inbox is unavailable. " +
            "Check the Financial Tracker App Group signing entitlement.");
#else
        var directory = Path.Combine(
            FileSystem.AppDataDirectory,
            "FinancialTrackerShared");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, DatabaseFileName);
#endif
    }
}
