namespace FinancialTracker.Models;

public sealed record CurrencyOption(
    string FlagAsset,
    string Code,
    string Name,
    string Symbol);
