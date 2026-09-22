using FinancialTracker.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FinancialTracker.Platforms.Windows;

public sealed class WindowsBackupFileSaver : IBackupFileSaver
{
    public async Task<bool> SaveAsync(
        string sourcePath,
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        var nativeWindow = Microsoft.Maui.Controls.Application.Current?
            .Windows
            .FirstOrDefault()?
            .Handler?
            .PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("The application window is unavailable.");

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName)
        };
        picker.FileTypeChoices.Add(
            "Financial Tracker database",
            [".db3"]);
        InitializeWithWindow.Initialize(
            picker,
            WindowNative.GetWindowHandle(nativeWindow));

        var destinationFile = await picker.PickSaveFileAsync();
        if (destinationFile is null)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using (var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true))
        await using (var destinationStream = await destinationFile.OpenStreamForWriteAsync())
        {
            destinationStream.SetLength(0);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            await destinationStream.FlushAsync(cancellationToken);
        }

        return true;
    }
}
