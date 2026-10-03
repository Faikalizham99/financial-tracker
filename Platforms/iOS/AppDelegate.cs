using Foundation;

using System.Globalization;

namespace FinancialTracker;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool OpenUrl(
        UIKit.UIApplication application,
        NSUrl url,
        NSDictionary options)
    {
        var host = url.Host?.ToLowerInvariant();
        if (host is not (
                "add-transaction" or
                "add-asset-snapshot" or
                "assets" or
                "transactions"))
        {
            return base.OpenUrl(application, url, options);
        }

        if (Microsoft.Maui.Controls.Application.Current is App app)
        {
            if (host == "add-transaction")
            {
                var amount = TryReadAmount(url.Query);
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(
                    () => app.RequestAddTransactionFromWidget(amount));
            }
            else if (host == "add-asset-snapshot")
            {
                var month = TryReadMonth(url.Query);
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(
                    () => app.RequestAddAssetSnapshotFromWidget(month));
            }
            else if (host == "assets")
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(
                    app.RequestOpenAssetsFromWidget);
            }
            else if (TryReadDate(url.Query) is DateTime date)
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(
                    () => app.RequestOpenTransactionsFromWidget(date));
            }
            return true;
        }

        return false;
    }

    private static DateTime? TryReadDate(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var part in query.TrimStart('?').Split('&'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 &&
                pair[0].Equals("date", StringComparison.OrdinalIgnoreCase) &&
                DateTime.TryParseExact(
                    Uri.UnescapeDataString(pair[1]),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                return date;
            }
        }

        return null;
    }

    private static DateTime? TryReadMonth(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var part in query.TrimStart('?').Split('&'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2 ||
                !pair[0].Equals("month", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(pair[1], out var monthKey))
            {
                continue;
            }

            var year = monthKey / 100;
            var month = monthKey % 100;
            if (year is >= 1900 and <= 9999 && month is >= 1 and <= 12)
            {
                return new DateTime(year, month, 1);
            }
        }

        return null;
    }

    private static decimal? TryReadAmount(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        foreach (var part in query.TrimStart('?').Split('&'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 &&
                pair[0].Equals("amount", StringComparison.OrdinalIgnoreCase) &&
                decimal.TryParse(
                    Uri.UnescapeDataString(pair[1]),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var amount) &&
                amount > 0)
            {
                return amount;
            }
        }

        return null;
    }
}
