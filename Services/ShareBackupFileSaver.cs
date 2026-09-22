using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace FinancialTracker.Services;

public sealed class ShareBackupFileSaver : IBackupFileSaver
{
    public async Task<bool> SaveAsync(
        string sourcePath,
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Save Financial Tracker backup",
            File = new ShareFile(sourcePath, "application/vnd.sqlite3")
        });
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }
}
