using FinancialTracker.Services;

namespace FinancialTracker;

public partial class App : Application
{
    private readonly MainPage mainPage;

    public App()
    {
        InitializeComponent();
        AppearanceService.ApplyStartupAppearance(this);
        mainPage = new MainPage();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(mainPage);
}
