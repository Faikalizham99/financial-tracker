using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class WidgetAppearanceCoordinator(IWidgetAppearancePublisher publisher)
{
    private readonly object synchronization = new();
    private WidgetAppearanceSnapshot? lastPublishedSnapshot;

    public void Publish(string theme, string accentColorHex)
    {
        if (!publisher.IsSupported)
        {
            return;
        }

        var snapshot = new WidgetAppearanceSnapshot(
            Version: 1,
            Theme: AppearanceValueNormalizer.NormalizeTheme(theme),
            AccentColorHex: AppearanceValueNormalizer.NormalizeAccentColor(
                accentColorHex));
        lock (synchronization)
        {
            if (snapshot == lastPublishedSnapshot)
            {
                return;
            }

            if (publisher.TryPublish(snapshot))
            {
                lastPublishedSnapshot = snapshot;
            }
        }
    }
}
