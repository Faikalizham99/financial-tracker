using FinancialTracker.Models;
using SQLite;

namespace FinancialTracker.Data;

public sealed class LocalDatabase
{
    private const int CurrentSchemaVersion = 2;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private SQLiteAsyncConnection? connection;
    private bool isInitialized;

    private SQLiteAsyncConnection Connection =>
        connection ??= new SQLiteAsyncConnection(
            Path.Combine(FileSystem.AppDataDirectory, "financial-tracker.db3"),
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
                await Connection.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion}");
            }
            else if (version < 2)
            {
                await Connection.ExecuteAsync(
                    "ALTER TABLE AppSettings ADD COLUMN AccentColorHex TEXT NOT NULL DEFAULT '#5044E4'");
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
}
