using FinancialTracker.Helpers;

namespace FinancialTracker.ViewModels;

public sealed partial class TransactionStatisticsViewModel
{
    private string FormatDaily(long amountMinor)
    {
        var source = GetSourceMonth();
        var dayCount = DateTime.DaysInMonth(source.Month.Year, source.Month.Month);
        return $"{FormatMoney(AveragePerDay(amountMinor, dayCount))} daily";
    }

    private string FormatMoney(long amountMinor) => MoneyFormatter.FormatMinor(
        amountMinor,
        data?.CurrencySymbol ?? string.Empty,
        separateSign: true);

    private string FormatDetailedComparison(
        long currentMinor,
        long? previousMinor,
        DateTime previousMonth)
    {
        if (!previousMinor.HasValue || previousMinor.Value == 0)
        {
            return currentMinor == 0
                ? $"No change from {previousMonth:MMM yyyy}"
                : $"No earlier value for {previousMonth:MMM yyyy}";
        }

        var difference = currentMinor - previousMinor.Value;
        var percentage = difference / (double)Math.Abs(previousMinor.Value) * 100d;
        var direction = difference >= 0 ? "higher" : "lower";
        return $"{FormatMoney(Math.Abs(difference))} {direction} than " +
            $"{previousMonth:MMM yyyy} \u00B7 {Math.Abs(percentage):0.#}%";
    }

    private static string FormatPercentage(long amountMinor, long totalMinor) =>
        totalMinor <= 0 ? "0%" : $"{amountMinor / (double)totalMinor * 100d:0.#}%";

    private static long AveragePerDay(long amountMinor, int dayCount) =>
        (long)Math.Round(amountMinor / (double)Math.Max(1, dayCount));

    private static string FormatCompactComparison(long currentMinor, long previousMinor)
    {
        if (previousMinor == 0)
        {
            return currentMinor == 0 ? "No monthly change" : "No earlier value";
        }

        var percentage = (currentMinor - previousMinor) /
            (double)Math.Abs(previousMinor) * 100d;
        return $"{percentage:+0.#;-0.#;0}% vs previous month";
    }

    private string FormatBreakdownChange(long differenceMinor, long previousMinor)
    {
        if (differenceMinor == 0)
        {
            return "No change";
        }

        if (previousMinor == 0)
        {
            return "New this month";
        }

        var arrow = differenceMinor > 0 ? "\u25B2" : "\u25BC";
        return $"{arrow} {FormatMoney(Math.Abs(differenceMinor))}";
    }
}
