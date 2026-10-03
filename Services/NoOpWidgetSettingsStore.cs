namespace FinancialTracker.Services;

public sealed class NoOpWidgetSettingsStore : IWidgetSettingsStore
{
    public bool TryReadIncludeInvestment(out bool includeInvestment)
    {
        includeInvestment = true;
        return false;
    }

    public bool TryWriteIncludeInvestment(bool includeInvestment) => true;
}
