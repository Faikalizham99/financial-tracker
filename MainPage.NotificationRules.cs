namespace FinancialTracker;

public partial class MainPage
{
    private async void OnNotificationRulesRequested() =>
        await NotificationRulesOverlay.OpenAsync();

    private void OnNotificationRulesVisibilityChanged(bool isVisible)
    {
        BottomNavigationDock.Opacity = isVisible ? 0 : 1;
        BottomNavigationDock.InputTransparent = isVisible;
        BottomNavigationDock.IsVisible = !isVisible;
    }
}
