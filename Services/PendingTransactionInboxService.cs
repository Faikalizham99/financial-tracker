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

        await Connection.ExecuteScalarAsync<int>("PRAGMA busy_timeout = 5000")
            .ConfigureAwait(false);
        await Connection.ExecuteScalarAsync<string>("PRAGMA journal_mode = WAL")
            .ConfigureAwait(false);
        await Connection.ExecuteScalarAsync<int>("PRAGMA synchronous = FULL")
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
        string groupIdentifier;
        try
        {
            var appBundle = NSBundle.MainBundle;
            static string Executable(NSBundle? bundle) =>
                bundle?.ExecutableUrl?.Path ??
                throw new IOException("A Financial Tracker extension is missing.");
            var widgetBundle = NSBundle.FromPath(Path.Combine(
                appBundle.BundlePath,
                "PlugIns",
                "FinancialTrackerWidgetExtension.appex"));
            var intentsBundle = NSBundle.FromPath(Path.Combine(
                appBundle.BundlePath,
                "Extensions",
                "FinancialTrackerIntentsExtension.appex"));
            groupIdentifier = SignedAppGroups.SelectCommon(
                new[]
                {
                    Executable(appBundle),
                    Executable(widgetBundle),
                    Executable(intentsBundle)
                }.Select(SignedAppGroups.Read));
        }
        catch (AppGroupConfigurationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AppGroupConfigurationException(
                "Financial Tracker could not read the App Group information " +
                "from its signed app and extensions. Re-sign the complete IPA.",
                exception);
        }

        var container = NSFileManager.DefaultManager.GetContainerUrl(groupIdentifier)
            ?? throw new AppGroupConfigurationException(
                $"iOS cannot open the common signed App Group ({groupIdentifier}). " +
                "Re-sign the complete IPA and unlock the iPhone once after restart.");
        var containerPath = container.Path ??
            throw new AppGroupConfigurationException(
                "The common App Group container has no accessible path.");
        var directory = Path.Combine(containerPath, "Library", "FinancialTracker");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, DatabaseFileName);
#else
        var directory = Path.Combine(
            FileSystem.AppDataDirectory,
            "FinancialTrackerShared");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, DatabaseFileName);
#endif
    }
}
