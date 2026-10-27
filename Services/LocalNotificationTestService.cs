#if IOS
using Foundation;
using UserNotifications;
#endif

namespace FinancialTracker.Services;

public readonly record struct LocalNotificationTestResult(
    bool IsSuccess,
    string Message);

public static class LocalNotificationTestService
{
    public static async Task<LocalNotificationTestResult> SendAsync(
        string message)
    {
#if IOS
        var center = UNUserNotificationCenter.Current;
        var permission = await EnsureAuthorizationAsync();
        if (!permission.IsSuccess)
        {
            return permission;
        }

        var content = new UNMutableNotificationContent
        {
            Title = "Financial Tracker test",
            Body = message.Trim(),
            Sound = UNNotificationSound.Default
        };
        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, false);
        var request = UNNotificationRequest.FromIdentifier(
            $"financial-tracker-test-{Guid.NewGuid():N}",
            content,
            trigger);
        var error = await AddRequestAsync(center, request);
        return error is null
            ? new LocalNotificationTestResult(
                true,
                "Test notification scheduled. It should appear in about one second.")
            : new LocalNotificationTestResult(
                false,
                $"The test notification could not be scheduled. {error.LocalizedDescription}");
#else
        await Task.CompletedTask;
        return new LocalNotificationTestResult(
            false,
            "Real test notifications are currently available on iOS only.");
#endif
    }

    public static async Task<LocalNotificationTestResult> EnsureAuthorizationAsync()
    {
#if IOS
        var permission = await RequestPermissionAsync(
            UNUserNotificationCenter.Current);
        return permission.IsApproved
            ? new LocalNotificationTestResult(
                true,
                "Financial Tracker notifications are enabled.")
            : new LocalNotificationTestResult(
                false,
                permission.ErrorMessage ??
                "Notifications are disabled. Enable them for Financial Tracker in iOS Settings.");
#else
        await Task.CompletedTask;
        return new LocalNotificationTestResult(
            false,
            "Capture confirmation notifications are available on iOS only.");
#endif
    }

#if IOS
    private static Task<(bool IsApproved, string? ErrorMessage)> RequestPermissionAsync(
        UNUserNotificationCenter center)
    {
        var completion = new TaskCompletionSource<(
            bool IsApproved,
            string? ErrorMessage)>();
        center.RequestAuthorization(
            UNAuthorizationOptions.Alert |
            UNAuthorizationOptions.Sound |
            UNAuthorizationOptions.Badge,
            (approved, error) => completion.TrySetResult((
                approved,
                error?.LocalizedDescription)));
        return completion.Task;
    }

    private static Task<NSError?> AddRequestAsync(
        UNUserNotificationCenter center,
        UNNotificationRequest request)
    {
        var completion = new TaskCompletionSource<NSError?>();
        center.AddNotificationRequest(
            request,
            error => completion.TrySetResult(error));
        return completion.Task;
    }
#endif
}
