using Foundation;

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
        if (url.AbsoluteString is not
            "com.faikalizham.financial-tracker://add-transaction")
        {
            return base.OpenUrl(application, url, options);
        }

        if (Microsoft.Maui.Controls.Application.Current is App app)
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(
                app.RequestAddTransactionFromWidget);
            return true;
        }

        return false;
    }
}
