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
            var containerPath = GetAvailableContainerPath();
            if (containerPath is null)
            {
                Debug.WriteLine(
                    "No configured Widget App Group container is available: " +
                    string.Join(", ", WidgetConstants.AppGroupIdentifiers));
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

    private static string? GetAvailableContainerPath()
    {
        foreach (var identifier in WidgetConstants.AppGroupIdentifiers)
        {
            var containerUrl = NSFileManager.DefaultManager.GetContainerUrl(
                identifier);
            if (containerUrl?.Path is string containerPath)
            {
                return containerPath;
            }
        }

        return null;
    }
}
