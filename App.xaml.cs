namespace FinancialTracker;

public partial class App : Application
{
    private readonly MainPage mainPage;

    public App()
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        mainPage = new MainPage();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(mainPage);
}
