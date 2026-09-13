using FinancialTracker.Data;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class SettingsService(LocalDatabase database)
{
    public Task<AppSettingsRecord> GetAsync() => database.GetSettingsAsync();

    public Task SaveAsync(AppSettingsRecord settings) =>
        database.SaveSettingsAsync(settings);
}
