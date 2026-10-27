namespace FinancialTracker.Services;

public static class CaptureNotificationSettings
{
    public const string PreferenceFileName =
        "financial_tracker_notify_after_capture.txt";

    public static bool TryRead(out bool enabled, out string? error)
    {
        enabled = false;
        error = null;
        try
        {
            var path = ResolvePath();
            if (!File.Exists(path))
            {
                return true;
            }

            enabled = File.ReadAllText(path)
                .Trim()
                .Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryWrite(bool enabled, out string? error)
    {
        error = null;
        string? temporaryPath = null;
        try
        {
            var path = ResolvePath();
            temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporaryPath, enabled.ToString());
            File.Move(temporaryPath, path, overwrite: true);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryPath) &&
                File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // A stale temporary preference does not affect the saved value.
                }
            }
        }
    }

    private static string ResolvePath() => Path.Combine(
        PendingTransactionInboxService.ResolveSharedDirectory(),
        PreferenceFileName);
}
