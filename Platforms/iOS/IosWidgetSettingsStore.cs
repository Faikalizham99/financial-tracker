using System.Diagnostics;
using FinancialTracker.Services;
using Foundation;

namespace FinancialTracker.Platforms.iOS;

public sealed class IosWidgetSettingsStore : IWidgetSettingsStore
{
    public bool TryReadIncludeInvestment(out bool includeInvestment)
    {
        includeInvestment = true;
        try
        {
            var settingsPath = GetSettingsPath();
            if (settingsPath is null || !File.Exists(settingsPath))
            {
                return false;
            }

            return bool.TryParse(
                File.ReadAllText(settingsPath).Trim(),
                out includeInvestment);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Widget settings reading failed: {exception}");
            return false;
        }
    }

    public bool TryWriteIncludeInvestment(bool includeInvestment)
    {
        try
        {
            var settingsPath = GetSettingsPath();
            if (settingsPath is null)
            {
                return false;
            }

            var containerPath = Path.GetDirectoryName(settingsPath)!;
            Directory.CreateDirectory(containerPath);
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                includeInvestment ? bool.TrueString : bool.FalseString);
            File.Move(temporaryPath, settingsPath, overwrite: true);
            return true;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Widget settings writing failed: {exception}");
            return false;
        }
    }

    private static string? GetSettingsPath()
    {
        foreach (var identifier in WidgetConstants.AppGroupIdentifiers)
        {
            var containerUrl = NSFileManager.DefaultManager.GetContainerUrl(
                identifier);
            if (containerUrl?.Path is string containerPath)
            {
                return Path.Combine(
                    containerPath,
                    WidgetConstants.InvestmentPreferenceFileName);
            }
        }

        return null;
    }
}
