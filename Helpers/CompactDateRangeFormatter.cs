using System.Globalization;

namespace FinancialTracker.Helpers;

public static class CompactDateRangeFormatter
{
    public static string Format(DateTime startDate, DateTime endDate)
    {
        if (startDate == endDate)
        {
            return startDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }

        if (startDate.Year == endDate.Year && startDate.Month == endDate.Month)
        {
            return $"{startDate.Day}–{endDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";
        }

        if (startDate.Year == endDate.Year)
        {
            return $"{startDate.ToString("d MMM", CultureInfo.CurrentCulture)} – " +
                endDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }

        return $"{startDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture)} – " +
            endDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
    }
}
