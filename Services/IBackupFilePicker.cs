using Microsoft.Maui.Storage;

namespace FinancialTracker.Services;

public interface IBackupFilePicker
{
    Task<FileResult?> PickAsync();
}
