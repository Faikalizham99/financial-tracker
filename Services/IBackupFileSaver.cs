namespace FinancialTracker.Services;

public interface IBackupFileSaver
{
    Task<bool> SaveAsync(
        string sourcePath,
        string suggestedFileName,
        CancellationToken cancellationToken = default);
}
