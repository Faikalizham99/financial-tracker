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

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(mainPage);
}
