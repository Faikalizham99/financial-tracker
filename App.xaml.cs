using FinancialTracker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialTracker;

public partial class App : Application
{
    private readonly MainPage mainPage;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        AppearanceService.ApplyStartupAppearance(this);
        mainPage = services.GetRequiredService<MainPage>();
    }

    internal void RequestAddTransactionFromWidget() =>
        mainPage.RequestAddTransactionFromWidget();

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(mainPage);
        window.Resumed += OnWindowResumed;
        return window;
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        try
        {
            await mainPage.RefreshAfterResumeAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Refresh after app resume failed: {exception}");
            // The next resume or explicit data operation can retry safely.
        }
    }
}
