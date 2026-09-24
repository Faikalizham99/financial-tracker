using FinancialTracker.Models;
using SQLite;

namespace FinancialTracker.Data;

public sealed class LocalDatabase
{
    private const int CurrentSchemaVersion = 6;
    private const string KnownTransactionDataPreferenceKey =
        "database_has_known_transaction_data";
    private static readonly TimeSpan[] StartupEmptyReadRetryDelays =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1000),
        TimeSpan.FromMilliseconds(2000),
        TimeSpan.FromMilliseconds(3000)
    ];
    private static readonly TimeSpan[] IosDatabaseDiscoveryRetryDelays =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1000)
    ];
    private readonly SemaphoreSlim databaseLock = new(1, 1);
    private SQLiteAsyncConnection? connection;
    private bool isInitialized;

    internal static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, "financial-tracker.db3");

    private SQLiteAsyncConnection Connection =>
        connection ??= new SQLiteAsyncConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.FullMutex);

    public async Task InitializeAsync()
    {
        await databaseLock.WaitAsync();
        try
        {
            await EnsureInitializedCoreAsync();
        }
        finally
        {
            databaseLock.Release();
        }
    }

    public Task<AppSettingsRecord> GetSettingsAsync() =>
        ExecuteWithConnectionAsync(async activeConnection =>
        {
            var settings = await activeConnection
                .Table<AppSettingsRecord>()
                .Where(item => item.Id == 1)
                .FirstOrDefaultAsync();

            if (settings is not null)
            {
                return settings;
            }

            settings = new AppSettingsRecord();
            await activeConnection.InsertAsync(settings);
            return settings;
        });

    public Task SaveSettingsAsync(AppSettingsRecord settings)
    {
        settings.Id = 1;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        return ExecuteWithConnectionAsync(
            activeConnection => activeConnection.InsertOrReplaceAsync(settings));
    }

    public Task<int> SaveTransactionAsync(TransactionRecord transaction) =>
        ExecuteWithConnectionAsync(
            activeConnection => activeConnection.InsertAsync(transaction));

    public Task<int> UpdateTransactionAsync(TransactionRecord transaction) =>
        ExecuteWithConnectionAsync(
            activeConnection => activeConnection.UpdateAsync(transaction));

    public Task<int> DeleteTransactionAsync(int transactionId) =>
        ExecuteWithConnectionAsync(activeConnection => activeConnection.ExecuteAsync(
            "DELETE FROM Transactions WHERE Id = ?",
            transactionId));

    public Task<TransactionRecord?> GetTransactionAsync(int transactionId) =>
        ExecuteWithConnectionAsync(async activeConnection =>
            (TransactionRecord?)await activeConnection.FindAsync<TransactionRecord>(transactionId));

    public Task<IReadOnlyList<TransactionRecord>> GetTransactionsAsync() =>
        ExecuteWithConnectionAsync(QueryTransactionsCoreAsync);

    public Task<MonthlyBudgetRecord?> GetMonthlyBudgetAsync(int monthKey) =>
        ExecuteWithConnectionAsync(async activeConnection =>
            (MonthlyBudgetRecord?)await activeConnection.FindAsync<MonthlyBudgetRecord>(monthKey));

    public Task<IReadOnlyList<MonthlyBudgetRecord>> GetMonthlyBudgetsAsync(
        int firstMonthKey,
        int lastMonthKey) =>
        ExecuteWithConnectionAsync<IReadOnlyList<MonthlyBudgetRecord>>(async activeConnection =>
            await activeConnection.Table<MonthlyBudgetRecord>()
                .Where(item =>
                    item.MonthKey >= firstMonthKey &&
                    item.MonthKey <= lastMonthKey)
                .OrderBy(item => item.MonthKey)
                .ToListAsync());

    public Task SaveMonthlyBudgetAsync(MonthlyBudgetRecord budget) =>
        ExecuteWithConnectionAsync(
            activeConnection => activeConnection.InsertOrReplaceAsync(budget));

    public async Task<IReadOnlyList<TransactionRecord>> GetTransactionsForStartupAsync(
        CancellationToken cancellationToken = default)
    {
        await WaitForExistingIosDatabaseAsync(cancellationToken);

        var hasKnownTransactionData = Preferences.Default.Get(
            KnownTransactionDataPreferenceKey,
            false);
        var shouldRecoverEmptyRead =
            File.Exists(DatabasePath) || hasKnownTransactionData;

        await databaseLock.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 0;
                 attempt <= StartupEmptyReadRetryDelays.Length;
                 attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(
                        StartupEmptyReadRetryDelays[attempt - 1],
                        cancellationToken);
                }

                if (!File.Exists(DatabasePath) &&
                    shouldRecoverEmptyRead &&
                    attempt < StartupEmptyReadRetryDelays.Length)
                {
                    continue;
                }

                await EnsureInitializedCoreAsync();
                var records = await QueryTransactionsCoreAsync(Connection);
                if (records.Count > 0 ||
                    !shouldRecoverEmptyRead)
                {
                    return records;
                }

                if (attempt == StartupEmptyReadRetryDelays.Length)
                {
                    if (hasKnownTransactionData)
                    {
                        throw new InvalidDataException(
                            "The saved database is temporarily unavailable. An empty startup result was rejected.");
                    }

                    // An existing database can legitimately contain no
                    // transactions. Only accept that after the file has had
                    // the full availability window to settle.
                    return records;
                }

                // A LiveContainer data directory or externally restored database can
                // replace the file after SQLite has opened it. Reopening ensures the
                // next attempt observes the current file instead of the stale handle.
                await CloseConnectionCoreAsync();
            }

            return [];
        }
        finally
        {
            databaseLock.Release();
        }
    }

    public Task CreateBackupAsync(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        return ExecuteWithConnectionAsync(async activeConnection =>
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            await activeConnection.BackupAsync(destinationPath, "main");
            ValidateDatabaseFile(destinationPath);
        });
    }

    public async Task RestoreFromBackupAsync(
        Stream backupStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backupStream);

        var databaseDirectory = Path.GetDirectoryName(DatabasePath)
            ?? throw new InvalidOperationException("The database directory is unavailable.");
        Directory.CreateDirectory(databaseDirectory);

        var operationId = Guid.NewGuid().ToString("N");
        var stagingPath = Path.Combine(
            databaseDirectory,
            $".financial-tracker-restore-{operationId}.db3");
        var rollbackPath = Path.Combine(
            databaseDirectory,
            $".financial-tracker-rollback-{operationId}.db3");

        try
        {
            await using (var stagingStream = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await backupStream.CopyToAsync(stagingStream, cancellationToken);
                await stagingStream.FlushAsync(cancellationToken);
            }

            ValidateDatabaseFile(stagingPath);

            await databaseLock.WaitAsync(cancellationToken);
            try
            {
                await ReplaceDatabaseCoreAsync(stagingPath, rollbackPath);
            }
            finally
            {
                databaseLock.Release();
            }
        }
        finally
        {
            TryDeleteFile(stagingPath);
            TryDeleteFile(rollbackPath);
        }
    }

    private async Task EnsureInitializedCoreAsync()
    {
        if (isInitialized)
        {
            return;
        }

        var version = await Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
        if (version > CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"This database was created by a newer Financial Tracker version (schema {version}).");
        }

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

        if (version < 5)
        {
            await Connection.CreateTableAsync<MonthlyBudgetRecord>();
        }
        else if (version == 5)
        {
            await MigratePrototypeMonthlyBudgetsAsync();
        }

        if (version < CurrentSchemaVersion)
        {
            await Connection.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion}");
        }

        isInitialized = true;
    }

    private async Task MigratePrototypeMonthlyBudgetsAsync()
    {
        await Connection.CreateTableAsync<MonthlyBudgetRecord>();
        var columns = await Connection.GetTableInfoAsync("MonthlyBudgets");
        var columnNames = columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columnNames.Contains(nameof(MonthlyBudgetRecord.BudgetIncludingInvestmentMinor)))
        {
            await Connection.ExecuteAsync(
                "ALTER TABLE MonthlyBudgets ADD COLUMN " +
                "BudgetIncludingInvestmentMinor INTEGER NOT NULL DEFAULT 0");
        }

        if (!columnNames.Contains(nameof(MonthlyBudgetRecord.BudgetExcludingInvestmentMinor)))
        {
            await Connection.ExecuteAsync(
                "ALTER TABLE MonthlyBudgets ADD COLUMN " +
                "BudgetExcludingInvestmentMinor INTEGER NOT NULL DEFAULT 0");
        }

        if (columnNames.Contains("AmountMinor"))
        {
            await Connection.ExecuteAsync(
                "UPDATE MonthlyBudgets SET " +
                "BudgetIncludingInvestmentMinor = AmountMinor, " +
                "BudgetExcludingInvestmentMinor = AmountMinor");
        }
    }

    private async Task ReplaceDatabaseCoreAsync(string stagingPath, string rollbackPath)
    {
        await EnsureInitializedCoreAsync();
        await Connection.BackupAsync(rollbackPath, "main");
        await CloseConnectionCoreAsync();

        try
        {
            DeleteCompanionFiles(DatabasePath);
            File.Move(stagingPath, DatabasePath, overwrite: true);
            ValidateDatabaseFile(DatabasePath);
            await EnsureInitializedCoreAsync();
        }
        catch
        {
            await CloseConnectionCoreAsync();
            DeleteCompanionFiles(DatabasePath);

            if (File.Exists(rollbackPath))
            {
                File.Move(rollbackPath, DatabasePath, overwrite: true);
            }

            await EnsureInitializedCoreAsync();
            throw;
        }
    }

    private async Task CloseConnectionCoreAsync()
    {
        var activeConnection = connection;
        connection = null;
        isInitialized = false;

        if (activeConnection is not null)
        {
            await activeConnection.CloseAsync();
        }
    }

    private async Task<T> ExecuteWithConnectionAsync<T>(
        Func<SQLiteAsyncConnection, Task<T>> operation)
    {
        await databaseLock.WaitAsync();
        try
        {
            await EnsureInitializedCoreAsync();
            return await operation(Connection);
        }
        finally
        {
            databaseLock.Release();
        }
    }

    private async Task ExecuteWithConnectionAsync(
        Func<SQLiteAsyncConnection, Task> operation)
    {
        await databaseLock.WaitAsync();
        try
        {
            await EnsureInitializedCoreAsync();
            await operation(Connection);
        }
        finally
        {
            databaseLock.Release();
        }
    }

    private static async Task<IReadOnlyList<TransactionRecord>> QueryTransactionsCoreAsync(
        SQLiteAsyncConnection activeConnection)
    {
        var records = await activeConnection.QueryAsync<TransactionRecord>(
            "SELECT * FROM Transactions " +
            "ORDER BY TransactionDate DESC, CreatedAtUtc DESC, Id DESC");
        RememberTransactionData(records);
        return records;
    }

    private static async Task WaitForExistingIosDatabaseAsync(
        CancellationToken cancellationToken)
    {
        if (DeviceInfo.Platform != DevicePlatform.iOS || File.Exists(DatabasePath))
        {
            return;
        }

        // LiveContainer can expose the app data directory shortly after MAUI
        // starts. Do not create a replacement empty database during that
        // window. This delay only affects iOS when no database is initially
        // visible (including a genuine first launch).
        foreach (var delay in IosDatabaseDiscoveryRetryDelays)
        {
            await Task.Delay(delay, cancellationToken);
            if (File.Exists(DatabasePath))
            {
                return;
            }
        }
    }

    private static void RememberTransactionData(
        IReadOnlyCollection<TransactionRecord> records)
    {
        if (records.Count > 0)
        {
            Preferences.Default.Set(KnownTransactionDataPreferenceKey, true);
        }
    }

    private static void ValidateDatabaseFile(string databasePath)
    {
        if (!File.Exists(databasePath) || new FileInfo(databasePath).Length == 0)
        {
            throw new InvalidDataException("The selected backup is empty.");
        }

        try
        {
            using var candidate = new SQLiteConnection(
                databasePath,
                SQLiteOpenFlags.ReadOnly);
            var integrityResult = candidate.ExecuteScalar<string>("PRAGMA integrity_check");
            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The selected backup failed SQLite integrity validation.");
            }

            var version = candidate.ExecuteScalar<int>("PRAGMA user_version");
            if (version > CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"The selected backup uses newer schema version {version}.");
            }

            var hasSettingsTable = TableExists(candidate, "AppSettings");
            var hasTransactionsTable = TableExists(candidate, "Transactions");
            var hasMonthlyBudgetsTable = TableExists(candidate, "MonthlyBudgets");
            if (!hasSettingsTable && !hasTransactionsTable)
            {
                throw new InvalidDataException(
                    "The selected file is not a Financial Tracker database.");
            }

            if ((version >= 1 && !hasSettingsTable) ||
                (version >= 3 && !hasTransactionsTable) ||
                (version >= 5 && !hasMonthlyBudgetsTable))
            {
                throw new InvalidDataException(
                    "The selected backup is missing required Financial Tracker tables.");
            }
        }
        catch (SQLiteException exception)
        {
            throw new InvalidDataException(
                "The selected file is not a readable SQLite backup.",
                exception);
        }
    }

    private static bool TableExists(SQLiteConnection candidate, string tableName) =>
        candidate.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?",
            tableName) > 0;

    private static void DeleteCompanionFiles(string databasePath)
    {
        DeleteFileIfExists($"{databasePath}-wal");
        DeleteFileIfExists($"{databasePath}-shm");
        DeleteFileIfExists($"{databasePath}-journal");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            DeleteFileIfExists(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
