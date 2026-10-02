using FinancialTracker.Models;

namespace FinancialTracker.Services;

public interface IWidgetAppearancePublisher
{
    bool IsSupported { get; }

    bool TryPublish(WidgetAppearanceSnapshot snapshot);
}
