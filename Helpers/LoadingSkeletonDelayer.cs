using System.Diagnostics;

namespace FinancialTracker.Helpers;

public static class LoadingSkeletonDelayer
{
    private const int DisplayDelayMilliseconds = 140;
    private const int MinimumVisibleMilliseconds = 240;

    public static async Task RunAsync(
        Func<Task> operation,
        Action<bool> setVisible)
        => await RunAsync(
            operation,
            visible =>
            {
                setVisible(visible);
                return Task.CompletedTask;
            });

    public static async Task RunAsync(
        Func<Task> operation,
        Func<bool, Task> setVisible)
    {
        var operationTask = operation();
        var shownAt = await ShowWhenNeededAsync(operationTask, setVisible);

        try
        {
            await operationTask;
        }
        finally
        {
            await HideWhenReadyAsync(shownAt, setVisible);
        }
    }

    public static async Task<T> RunAsync<T>(
        Func<Task<T>> operation,
        Action<bool> setVisible)
        => await RunAsync(
            operation,
            visible =>
            {
                setVisible(visible);
                return Task.CompletedTask;
            });

    public static async Task<T> RunAsync<T>(
        Func<Task<T>> operation,
        Func<bool, Task> setVisible)
    {
        var operationTask = operation();
        var shownAt = await ShowWhenNeededAsync(operationTask, setVisible);

        try
        {
            return await operationTask;
        }
        finally
        {
            await HideWhenReadyAsync(shownAt, setVisible);
        }
    }

    private static async Task<long?> ShowWhenNeededAsync(
        Task operationTask,
        Func<bool, Task> setVisible)
    {
        var completedTask = await Task.WhenAny(
            operationTask,
            Task.Delay(DisplayDelayMilliseconds));
        if (completedTask == operationTask)
        {
            return null;
        }

        await setVisible(true);
        await Task.Yield();
        return Stopwatch.GetTimestamp();
    }

    private static async Task HideWhenReadyAsync(
        long? shownAt,
        Func<bool, Task> setVisible)
    {
        if (!shownAt.HasValue)
        {
            return;
        }

        var elapsed = Stopwatch.GetElapsedTime(shownAt.Value);
        var remaining = TimeSpan.FromMilliseconds(MinimumVisibleMilliseconds) - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }

        await setVisible(false);
    }
}
