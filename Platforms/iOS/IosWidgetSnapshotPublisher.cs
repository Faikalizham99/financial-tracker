using System.Diagnostics;
using System.Text.Json;
using FinancialTracker.Models;
using FinancialTracker.Services;
using Foundation;

namespace FinancialTracker.Platforms.iOS;

public sealed class IosWidgetSnapshotPublisher : IWidgetSnapshotPublisher
{
    private readonly WidgetKit.WidgetCenterProxy widgetCenter = new();

    public bool TryPublish(WidgetSnapshot snapshot)
    {
        try
        {
            var containerUrl = NSFileManager.DefaultManager.GetContainerUrl(
                WidgetConstants.AppGroupIdentifier);
            if (containerUrl?.Path is not string containerPath)
            {
                Debug.WriteLine(
                    "Widget App Group container is unavailable: " +
                    WidgetConstants.AppGroupIdentifier);
                return false;
            }

            Directory.CreateDirectory(containerPath);
            var snapshotPath = Path.Combine(
                containerPath,
                WidgetConstants.SnapshotFileName);
            var temporaryPath = snapshotPath + ".tmp";
            var json = JsonSerializer.Serialize(
                snapshot,
                WidgetJsonSerializerContext.Default.WidgetSnapshot);

            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, snapshotPath, overwrite: true);
            widgetCenter.ReloadAllTimeLines();
            return true;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Widget snapshot publishing failed: {exception}");
            return false;
        }
    }
}
