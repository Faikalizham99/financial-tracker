using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class NoOpWidgetAppearancePublisher : IWidgetAppearancePublisher
{
    public bool IsSupported => false;

    public bool TryPublish(WidgetAppearanceSnapshot snapshot) => true;
}
