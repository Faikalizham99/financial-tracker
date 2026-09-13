namespace FinancialTracker;

using FinancialTracker.ViewModels;
using FinancialTracker.Data;
using FinancialTracker.Services;

public partial class MainPage : ContentPage
{
    private readonly SettingsViewModel settingsViewModel;

    public MainPage()
        : this(new SettingsViewModel(new SettingsService(new LocalDatabase())))
    {
    }

    public MainPage(SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        this.settingsViewModel = settingsViewModel;
        BindingContext = settingsViewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e) =>
        await settingsViewModel.InitializeAsync();

    private void OnDashboardTapped(object? sender, TappedEventArgs e) => ShowSection(0);

    private void OnExpensesTapped(object? sender, TappedEventArgs e) => ShowSection(1);

    private void OnSettingsTapped(object? sender, TappedEventArgs e) => ShowSection(2);

    private void ShowSection(int selectedIndex)
    {
        DashboardView.IsVisible = selectedIndex == 0;
        ExpensesView.IsVisible = selectedIndex == 1;
        SettingsView.IsVisible = selectedIndex == 2;

        var tabs = new[] { DashboardTab, ExpensesTab, SettingsTab };
        var icons = new[] { DashboardIcon, ExpensesIcon, SettingsIcon };
        var labels = new[] { DashboardLabel, ExpensesLabel, SettingsLabel };

        for (var index = 0; index < tabs.Length; index++)
        {
            var isSelected = index == selectedIndex;
            tabs[index].Style = (Style)Application.Current!.Resources[
                isSelected ? "SelectedNavTab" : "NavTab"];
            icons[index].Style = (Style)Application.Current.Resources[
                isSelected ? "SelectedNavLabel" : "NavLabel"];
            labels[index].Style = (Style)Application.Current.Resources[
                isSelected ? "SelectedNavLabel" : "NavLabel"];

            icons[index].FontSize = index switch
            {
                0 => 21,
                1 => 20,
                _ => 19
            };
        }
    }
}
