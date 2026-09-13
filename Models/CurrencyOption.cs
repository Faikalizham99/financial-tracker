namespace FinancialTracker.Models;

public sealed record CurrencyOption(
    string Flag,
    string FlagAsset,
    string Code,
    string Name,
    string Symbol)
{
    public string DisplayText => $"{Flag}  {Code} - {Name}";

    public string SymbolDescription => $"Shown as {Symbol}";
}
