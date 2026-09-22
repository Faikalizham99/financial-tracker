using FinancialTracker.Services;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using UniformTypeIdentifiers;
using UIKit;

namespace FinancialTracker.Platforms.iOS;

public sealed class IosBackupFilePicker : IBackupFilePicker
{
    public async Task<FileResult?> PickAsync()
    {
        var completion = new TaskCompletionSource<FileResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var picker = new UIDocumentPickerViewController(
            [UTTypes.Data],
            asCopy: true)
        {
            AllowsMultipleSelection = false,
            ShouldShowFileExtensions = true
        };
        var pickerDelegate = new BackupPickerDelegate(completion);
        picker.Delegate = pickerDelegate;

        var presentingController = Platform.GetCurrentUIViewController()
            ?? throw new InvalidOperationException(
                "The current iOS view controller is unavailable.");
        presentingController.PresentViewController(picker, true, null);

        return await completion.Task;
    }

    [Preserve(AllMembers = true)]
    private sealed class BackupPickerDelegate(
        TaskCompletionSource<FileResult?> completion) : UIDocumentPickerDelegate
    {
        public override void DidPickDocument(
            UIDocumentPickerViewController controller,
            NSUrl[] urls) => Complete(urls.FirstOrDefault());

        public override void DidPickDocument(
            UIDocumentPickerViewController controller,
            NSUrl url) => Complete(url);

        public override void WasCancelled(UIDocumentPickerViewController controller) =>
            completion.TrySetResult(null);

        private void Complete(NSUrl? url)
        {
            var localPath = url?.Path;
            if (string.IsNullOrWhiteSpace(localPath))
            {
                completion.TrySetResult(null);
                return;
            }

            completion.TrySetResult(new FileResult(localPath));
        }
    }
}
