using Microsoft.Maui.Storage;

namespace FinancialTracker;

public partial class MainPage
{
    private async Task OnDatabaseBackupRequested()
    {
        var backupPath = Path.Combine(
            FileSystem.CacheDirectory,
            $"financial-tracker-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db3");

        try
        {
            await RunWithDataLoadingSkeletonAsync(
                () => localDatabase.CreateBackupAsync(backupPath));

            await backupFileSaver.SaveAsync(
                backupPath,
                Path.GetFileName(backupPath));
        }
        catch
        {
            await DisplayAlertAsync(
                "Backup failed",
                "Financial Tracker could not create the backup. Your data was not changed.",
                "OK");
        }
        finally
        {
            TryDeleteTemporaryFile(backupPath);
        }
    }

    private async Task OnDatabaseRestoreRequested()
    {
        FileResult? selectedBackup;
        try
        {
            selectedBackup = await backupFilePicker.PickAsync();
        }
        catch
        {
            await DisplayAlertAsync(
                "Cannot open files",
                "Financial Tracker could not open the system file picker.",
                "OK");
            return;
        }

        if (selectedBackup is null)
        {
            return;
        }

        var shouldRestore = await DisplayAlertAsync(
            "Restore this backup?",
            $"{selectedBackup.FileName} will replace the current on-device transactions and settings. The file will be validated first.",
            "Restore",
            "Cancel");
        if (!shouldRestore)
        {
            return;
        }

        var databaseRestored = false;
        try
        {
            await using var backupStream = await selectedBackup.OpenReadAsync();
            await RunWithDataLoadingSkeletonAsync(async () =>
            {
                await localDatabase.RestoreFromBackupAsync(backupStream);
                databaseRestored = true;
                monthlyBudgetService.InvalidateCache();
                await settingsViewModel.ReloadAsync();
                await RefreshTransactionViewsAsync();
                await ReloadMonthlyBudgetCardsAsync();
                await AssetsView.RefreshAsync();
            });

            await DisplayAlertAsync(
                "Restore complete",
                "Your settings, budgets and transactions have been restored successfully.",
                "OK");
        }
        catch (InvalidDataException exception)
        {
            await DisplayAlertAsync(
                "Invalid backup",
                $"{exception.Message} Your existing data was not changed.",
                "OK");
        }
        catch
        {
            await DisplayAlertAsync(
                databaseRestored ? "Refresh required" : "Restore failed",
                databaseRestored
                    ? "The backup was restored, but the screen could not refresh. Close and reopen Financial Tracker to load the restored data."
                    : "Financial Tracker could not restore this backup. Your previous database has been recovered.",
                "OK");
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
