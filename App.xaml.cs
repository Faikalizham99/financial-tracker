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
        catch
        {
            // The next resume or explicit data operation can retry safely.
        }
    }
}
