using FinancialTracker.Models;
using SQLite;

namespace FinancialTracker.Data;

public sealed class LocalDatabase
{
    private const int CurrentSchemaVersion = 7;
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

    public Task<AppSettingsRecord> GetSettingsAsync() =>
        ExecuteWithConnectionAsync(async activeConnection =>
        {
            var settings = await activeConnection
                .Table<AppSettingsRecord>()
                .Where(item => item.Id == 1)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (settings is not null)
            {
                return settings;
            }

            settings = new AppSettingsRecord();
            await activeConnection.InsertAsync(settings).ConfigureAwait(false);
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
            (TransactionRecord?)await activeConnection.FindAsync<TransactionRecord>(transactionId)
                .ConfigureAwait(false));

    public Task<IReadOnlyList<TransactionRecord>> GetTransactionsAsync(
        DateTime startDateInclusive,
        DateTime endDateExclusive) =>
        ExecuteWithConnectionAsync<IReadOnlyList<TransactionRecord>>(
            activeConnection => QueryTransactionsCoreAsync(
                activeConnection,
                startDateInclusive.Date,
                endDateExclusive.Date));

    public Task<IReadOnlyList<TransactionRecord>> GetTransactionPageAsync(
        int offset,
        int limit) =>
        ExecuteWithConnectionAsync<IReadOnlyList<TransactionRecord>>(async activeConnection =>
            await activeConnection.QueryAsync<TransactionRecord>(
                "SELECT * FROM Transactions " +
                "ORDER BY TransactionDate DESC, CreatedAtUtc DESC, Id DESC " +
                "LIMIT ? OFFSET ?",
                Math.Max(1, limit),
                Math.Max(0, offset)).ConfigureAwait(false));

    public Task<TransactionDataSnapshot> GetTransactionSnapshotAsync(DateTime month) =>
        ExecuteWithConnectionAsync(
            activeConnection => QueryTransactionSnapshotCoreAsync(
                activeConnection,
                month));

    public Task<MonthlyBudgetRecord?> GetMonthlyBudgetAsync(int monthKey) =>
        ExecuteWithConnectionAsync(async activeConnection =>
            (MonthlyBudgetRecord?)await activeConnection.FindAsync<MonthlyBudgetRecord>(monthKey)
                .ConfigureAwait(false));

    public Task<IReadOnlyList<MonthlyBudgetRecord>> GetMonthlyBudgetsAsync(
        int firstMonthKey,
        int lastMonthKey) =>
        ExecuteWithConnectionAsync<IReadOnlyList<MonthlyBudgetRecord>>(async activeConnection =>
            await activeConnection.Table<MonthlyBudgetRecord>()
                .Where(item =>
                    item.MonthKey >= firstMonthKey &&
                    item.MonthKey <= lastMonthKey)
                .OrderBy(item => item.MonthKey)
                .ToListAsync()
                .ConfigureAwait(false));

    public Task SaveMonthlyBudgetAsync(MonthlyBudgetRecord budget) =>
        ExecuteWithConnectionAsync(
            activeConnection => activeConnection.InsertOrReplaceAsync(budget));

    public Task<AssetSnapshotData?> GetAssetSnapshotAsync(int monthKey) =>
        ExecuteWithConnectionAsync(async activeConnection =>
        {
            var snapshot = await activeConnection.Table<AssetSnapshotRecord>()
                .Where(item => item.MonthKey == monthKey)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            var values = await activeConnection.Table<AssetSnapshotValueRecord>()
                .Where(item => item.SnapshotId == snapshot.Id)
                .ToListAsync()
                .ConfigureAwait(false);
            return new AssetSnapshotData(snapshot, values);
        });

    public Task<IReadOnlyList<AssetSnapshotData>> GetAssetSnapshotsThroughAsync(
        int lastMonthKey,
        int limit) =>
        ExecuteWithConnectionAsync<IReadOnlyList<AssetSnapshotData>>(async activeConnection =>
        {
            var snapshots = await activeConnection.Table<AssetSnapshotRecord>()
                .Where(item => item.MonthKey <= lastMonthKey)
                .OrderByDescending(item => item.MonthKey)
                .Take(Math.Max(1, limit))
                .ToListAsync()
                .ConfigureAwait(false);
            if (snapshots.Count == 0)
            {
                return [];
            }

            var snapshotIds = string.Join(",", snapshots.Select(item => item.Id));
            var values = await activeConnection.QueryAsync<AssetSnapshotValueRecord>(
                $"SELECT * FROM AssetSnapshotValues WHERE SnapshotId IN ({snapshotIds})")
                .ConfigureAwait(false);
            var valuesBySnapshot = values.ToLookup(item => item.SnapshotId);
            return snapshots
                .Select(snapshot => new AssetSnapshotData(
                    snapshot,
                    valuesBySnapshot[snapshot.Id].ToList()))
                .ToList();
        });

    public Task SaveAssetSnapshotAsync(
        AssetSnapshotRecord snapshot,
        IReadOnlyList<AssetSnapshotValueRecord> values) =>
        ExecuteWithConnectionAsync(activeConnection =>
            activeConnection.RunInTransactionAsync(transaction =>
            {
                var now = DateTime.UtcNow;
                var existing = transaction.Table<AssetSnapshotRecord>()
                    .FirstOrDefault(item => item.MonthKey == snapshot.MonthKey);
                if (existing is null)
                {
                    snapshot.CreatedAtUtc = now;
                    snapshot.UpdatedAtUtc = now;
                    transaction.Insert(snapshot);
                }
                else
                {
                    snapshot.Id = existing.Id;
                    snapshot.CreatedAtUtc = existing.CreatedAtUtc;
                    snapshot.UpdatedAtUtc = now;
                    transaction.Update(snapshot);
                    transaction.Execute(
                        "DELETE FROM AssetSnapshotValues WHERE SnapshotId = ?",
                        snapshot.Id);
                }

                foreach (var value in values)
                {
                    value.Id = 0;
                    value.SnapshotId = snapshot.Id;
                    transaction.Insert(value);
                }
            }));

    public Task DeleteAssetSnapshotAsync(int monthKey) =>
        ExecuteWithConnectionAsync(activeConnection =>
            activeConnection.RunInTransactionAsync(transaction =>
            {
                var existing = transaction.Table<AssetSnapshotRecord>()
                    .FirstOrDefault(item => item.MonthKey == monthKey);
                if (existing is null)
                {
                    return;
                }

                transaction.Execute(
                    "DELETE FROM AssetSnapshotValues WHERE SnapshotId = ?",
                    existing.Id);
                transaction.Execute(
                    "DELETE FROM AssetSnapshots WHERE Id = ?",
                    existing.Id);
            }));

    public Task<TransactionDataSnapshot> GetTransactionSnapshotForStartupAsync(
        DateTime month,
        CancellationToken cancellationToken = default) =>
        ExecuteStartupReadAsync(
            activeConnection => QueryTransactionSnapshotCoreAsync(
                activeConnection,
                month),
            static snapshot => snapshot.DashboardRecords.Count > 0,
            cancellationToken);

    public Task CreateBackupAsync(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        return ExecuteWithConnectionAsync(async activeConnection =>
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            await activeConnection.BackupAsync(destinationPath, "main").ConfigureAwait(false);
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
                await backupStream.CopyToAsync(stagingStream, cancellationToken)
                    .ConfigureAwait(false);
                await stagingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            ValidateDatabaseFile(stagingPath);

            await databaseLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ReplaceDatabaseCoreAsync(stagingPath, rollbackPath)
                    .ConfigureAwait(false);
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

        var version = await Connection.ExecuteScalarAsync<int>("PRAGMA user_version")
            .ConfigureAwait(false);
        if (version > CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"This database was created by a newer Financial Tracker version (schema {version}).");
        }

        if (version < 1)
        {
            await Connection.CreateTableAsync<AppSettingsRecord>().ConfigureAwait(false);
        }
        else if (version < 2)
        {
            await Connection.ExecuteAsync(
                "ALTER TABLE AppSettings ADD COLUMN AccentColorHex TEXT NOT NULL DEFAULT '#5044E4'")
                .ConfigureAwait(false);
        }

        if (version < 3)
        {
            await Connection.CreateTableAsync<TransactionRecord>().ConfigureAwait(false);
        }

        if (version < 4)
        {
            await Connection.ExecuteAsync(
                "CREATE INDEX IF NOT EXISTS IX_Transactions_Date_Created_Id " +
                "ON Transactions (TransactionDate DESC, CreatedAtUtc DESC, Id DESC)")
                .ConfigureAwait(false);
        }

        if (version < 5)
        {
            await Connection.CreateTableAsync<MonthlyBudgetRecord>().ConfigureAwait(false);
        }
        else if (version == 5)
        {
            await MigratePrototypeMonthlyBudgetsAsync().ConfigureAwait(false);
        }

        if (version < 7)
        {
            await Connection.CreateTableAsync<AssetSnapshotRecord>().ConfigureAwait(false);
            await Connection.CreateTableAsync<AssetSnapshotValueRecord>().ConfigureAwait(false);
            await Connection.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_AssetSnapshots_MonthKey " +
                "ON AssetSnapshots (MonthKey)")
                .ConfigureAwait(false);
            await Connection.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_AssetSnapshotValues_Snapshot_Asset " +
                "ON AssetSnapshotValues (SnapshotId, AssetKey)")
                .ConfigureAwait(false);
        }

        if (version < CurrentSchemaVersion)
        {
            await Connection.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion}")
                .ConfigureAwait(false);
        }

        isInitialized = true;
    }

    private async Task MigratePrototypeMonthlyBudgetsAsync()
    {
        await Connection.CreateTableAsync<MonthlyBudgetRecord>().ConfigureAwait(false);
        var columns = await Connection.GetTableInfoAsync("MonthlyBudgets")
            .ConfigureAwait(false);
        var columnNames = columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columnNames.Contains(nameof(MonthlyBudgetRecord.BudgetIncludingInvestmentMinor)))
        {
            await Connection.ExecuteAsync(
                "ALTER TABLE MonthlyBudgets ADD COLUMN " +
                "BudgetIncludingInvestmentMinor INTEGER NOT NULL DEFAULT 0")
                .ConfigureAwait(false);
        }

        if (!columnNames.Contains(nameof(MonthlyBudgetRecord.BudgetExcludingInvestmentMinor)))
        {
            await Connection.ExecuteAsync(
                "ALTER TABLE MonthlyBudgets ADD COLUMN " +
                "BudgetExcludingInvestmentMinor INTEGER NOT NULL DEFAULT 0")
                .ConfigureAwait(false);
        }

        if (columnNames.Contains("AmountMinor"))
        {
            await Connection.ExecuteAsync(
                "UPDATE MonthlyBudgets SET " +
                "BudgetIncludingInvestmentMinor = AmountMinor, " +
                "BudgetExcludingInvestmentMinor = AmountMinor")
                .ConfigureAwait(false);
        }
    }

    private async Task ReplaceDatabaseCoreAsync(string stagingPath, string rollbackPath)
    {
        await EnsureInitializedCoreAsync().ConfigureAwait(false);
        await Connection.BackupAsync(rollbackPath, "main").ConfigureAwait(false);
        await CloseConnectionCoreAsync().ConfigureAwait(false);

        try
        {
            DeleteCompanionFiles(DatabasePath);
            File.Move(stagingPath, DatabasePath, overwrite: true);
            ValidateDatabaseFile(DatabasePath);
            await EnsureInitializedCoreAsync().ConfigureAwait(false);
        }
        catch
        {
            await CloseConnectionCoreAsync().ConfigureAwait(false);
            DeleteCompanionFiles(DatabasePath);

            if (File.Exists(rollbackPath))
            {
                File.Move(rollbackPath, DatabasePath, overwrite: true);
            }

            await EnsureInitializedCoreAsync().ConfigureAwait(false);
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
            await activeConnection.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task<T> ExecuteWithConnectionAsync<T>(
        Func<SQLiteAsyncConnection, Task<T>> operation)
    {
        await databaseLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureInitializedCoreAsync().ConfigureAwait(false);
            return await operation(Connection).ConfigureAwait(false);
        }
        finally
        {
            databaseLock.Release();
        }
    }

    private async Task ExecuteWithConnectionAsync(
        Func<SQLiteAsyncConnection, Task> operation)
    {
        await databaseLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureInitializedCoreAsync().ConfigureAwait(false);
            await operation(Connection).ConfigureAwait(false);
        }
        finally
        {
            databaseLock.Release();
        }
    }

    private async Task<T> ExecuteStartupReadAsync<T>(
        Func<SQLiteAsyncConnection, Task<T>> query,
        Func<T, bool> containsTransactions,
        CancellationToken cancellationToken)
    {
        await WaitForExistingIosDatabaseAsync(cancellationToken).ConfigureAwait(false);

        var hasKnownTransactionData = Preferences.Default.Get(
            KnownTransactionDataPreferenceKey,
            false);
        var shouldRecoverEmptyRead =
            File.Exists(DatabasePath) || hasKnownTransactionData;

        await databaseLock.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                        cancellationToken).ConfigureAwait(false);
                }

                if (!File.Exists(DatabasePath) &&
                    shouldRecoverEmptyRead &&
                    attempt < StartupEmptyReadRetryDelays.Length)
                {
                    continue;
                }

                await EnsureInitializedCoreAsync().ConfigureAwait(false);
                var result = await query(Connection).ConfigureAwait(false);
                if (containsTransactions(result) || !shouldRecoverEmptyRead)
                {
                    return result;
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
                    return result;
                }

                // A LiveContainer data directory or externally restored database can
                // replace the file after SQLite has opened it. Reopening ensures the
                // next attempt observes the current file instead of the stale handle.
                await CloseConnectionCoreAsync().ConfigureAwait(false);
            }

            throw new InvalidOperationException("The startup database read did not complete.");
        }
        finally
        {
            databaseLock.Release();
        }
    }

    private static async Task<IReadOnlyList<TransactionRecord>> QueryTransactionsCoreAsync(
        SQLiteAsyncConnection activeConnection,
        DateTime startDateInclusive,
        DateTime endDateExclusive)
    {
        var records = await activeConnection.QueryAsync<TransactionRecord>(
            "SELECT * FROM Transactions " +
            "WHERE TransactionDate >= ? AND TransactionDate < ? " +
            "ORDER BY TransactionDate DESC, CreatedAtUtc DESC, Id DESC",
            startDateInclusive,
            endDateExclusive).ConfigureAwait(false);
        RememberTransactionData(records);
        return records;
    }

    private static async Task<TransactionDataSnapshot> QueryTransactionSnapshotCoreAsync(
        SQLiteAsyncConnection activeConnection,
        DateTime month)
    {
        var monthStart = new DateTime(month.Year, month.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var nextMonthStart = monthStart.AddMonths(1);
        var comparisonRecords = await QueryTransactionsCoreAsync(
            activeConnection,
            previousMonthStart,
            nextMonthStart).ConfigureAwait(false);
        var recentRecords = await activeConnection.QueryAsync<TransactionRecord>(
            "SELECT * FROM Transactions " +
            "ORDER BY TransactionDate DESC, CreatedAtUtc DESC, Id DESC LIMIT 3")
            .ConfigureAwait(false);
        var descriptionHistoryRecords = await activeConnection.QueryAsync<TransactionRecord>(
            "SELECT * FROM Transactions " +
            "WHERE Description <> '' " +
            "ORDER BY TransactionDate DESC, CreatedAtUtc DESC, Id DESC LIMIT 250")
            .ConfigureAwait(false);

        var dashboardRecords = comparisonRecords
            .Concat(recentRecords)
            .DistinctBy(record => record.Id)
            .OrderByDescending(record => record.TransactionDate)
            .ThenByDescending(record => record.CreatedAtUtc)
            .ThenByDescending(record => record.Id)
            .ToList();
        var periodRecords = comparisonRecords
            .Where(record =>
                record.TransactionDate >= monthStart &&
                record.TransactionDate < nextMonthStart)
            .ToList();
        RememberTransactionData(dashboardRecords);

        return new TransactionDataSnapshot(
            dashboardRecords,
            periodRecords,
            descriptionHistoryRecords);
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
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
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
            var hasAssetSnapshotsTable = TableExists(candidate, "AssetSnapshots");
            var hasAssetSnapshotValuesTable = TableExists(candidate, "AssetSnapshotValues");
            if (!hasSettingsTable && !hasTransactionsTable)
            {
                throw new InvalidDataException(
                    "The selected file is not a Financial Tracker database.");
            }

            if ((version >= 1 && !hasSettingsTable) ||
                (version >= 3 && !hasTransactionsTable) ||
                (version >= 5 && !hasMonthlyBudgetsTable) ||
                (version >= 7 &&
                    (!hasAssetSnapshotsTable || !hasAssetSnapshotValuesTable)))
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
