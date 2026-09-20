using FinancialTracker.Models;
using SQLite;

namespace FinancialTracker.Data;

public sealed class LocalDatabase
{
    private const int CurrentSchemaVersion = 4;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private SQLiteAsyncConnection? connection;
    private bool isInitialized;

    internal static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, "financial-tracker.db3");

    private SQLiteAsyncConnection Connection =>
        connection ??= new SQLiteAsyncConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache);

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        await initializationLock.WaitAsync();
        try
        {
            if (isInitialized)
            {
                return;
            }

            var version = await Connection.ExecuteScalarAsync<int>("PRAGMA user_version");

            if (version < 1)
            {
                await Connection.CreateTableAsync<AppSettingsRecord>();
            }
            else if (version < 2)
            {
                await Connection.ExecuteAsync(
                    "ALTER TABLE AppSettings ADD COLUMN AccentColorHex TEXT NOT NULL DEFAULT '#5044E4'");
            }

            if (version < 3)
            {
                await Connection.CreateTableAsync<TransactionRecord>();
            }

            if (version < 4)
            {
                await Connection.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS IX_Transactions_Date_Created_Id " +
                    "ON Transactions (TransactionDate DESC, CreatedAtUtc DESC, Id DESC)");
            }

            if (version < CurrentSchemaVersion)
            {
                await Connection.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion}");
            }

            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public async Task<AppSettingsRecord> GetSettingsAsync()
    {
        await InitializeAsync();

        var settings = await Connection
            .Table<AppSettingsRecord>()
            .Where(item => item.Id == 1)
            .FirstOrDefaultAsync();

        if (settings is not null)
        {
            return settings;
        }

        settings = new AppSettingsRecord();
        await Connection.InsertAsync(settings);
        return settings;
    }

    public async Task SaveSettingsAsync(AppSettingsRecord settings)
    {
        await InitializeAsync();
        settings.Id = 1;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await Connection.InsertOrReplaceAsync(settings);
    }

    public async Task<int> SaveTransactionAsync(TransactionRecord transaction)
    {
        await InitializeAsync();
        return await Connection.InsertAsync(transaction);
    }

    public async Task<int> UpdateTransactionAsync(TransactionRecord transaction)
    {
        await InitializeAsync();
        return await Connection.UpdateAsync(transaction);
    }

    public async Task<int> DeleteTransactionAsync(int transactionId)
    {
        await InitializeAsync();
        return await Connection.ExecuteAsync(
            "DELETE FROM Transactions WHERE Id = ?",
            transactionId);
    }

    public async Task<TransactionRecord?> GetTransactionAsync(int transactionId)
    {
        await InitializeAsync();
        return await Connection.FindAsync<TransactionRecord>(transactionId);
    }

    public async Task<IReadOnlyList<TransactionRecord>> GetTransactionsAsync()
    {
        await InitializeAsync();
        return await Connection.QueryAsync<TransactionRecord>(
            "SELECT * FROM Transactions ORDER BY TransactionDate DESC, CreatedAtUtc DESC, Id DESC");
    }
}
