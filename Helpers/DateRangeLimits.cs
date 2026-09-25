namespace FinancialTracker.Helpers;

public static class DateRangeLimits
{
    public const int MinimumYear = 2000;
    public const int FutureYearCount = 10;

    public static DateTime MinimumDate { get; } = new(MinimumYear, 1, 1);

    public static DateTime MaximumDate => DateTime.Today.AddYears(FutureYearCount);

    public static int MaximumYear => DateTime.Today.Year + FutureYearCount;
}
