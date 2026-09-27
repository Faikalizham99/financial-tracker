namespace FinancialTracker.Helpers;

public static class MonthKeyConverter
{
    public static int FromDate(DateTime value) =>
        (value.Year * 100) + value.Month;

    public static DateTime ToDate(int monthKey) =>
        new(monthKey / 100, monthKey % 100, 1);
}
