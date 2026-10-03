namespace FinancialTracker.Services;

public interface IWidgetSettingsStore
{
    bool TryReadIncludeInvestment(out bool includeInvestment);

    bool TryWriteIncludeInvestment(bool includeInvestment);
}
