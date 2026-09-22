using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace FinancialTracker.Services;

public sealed class MauiBackupFilePicker : IBackupFilePicker
{
    private const string BackupDocumentType =
        "com.faikalizham.financial-tracker.backup";

    private static readonly FilePickerFileType BackupFileTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.MacCatalyst] =
                [BackupDocumentType, "public.database", "public.data"],
            [DevicePlatform.Android] =
                ["application/vnd.sqlite3", "application/x-sqlite3", "application/octet-stream"],
            [DevicePlatform.WinUI] = [".db3"]
        });

    public Task<FileResult?> PickAsync() =>
        FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose a Financial Tracker backup",
            FileTypes = BackupFileTypes
        });
}
