using Microsoft.Extensions.Logging;

using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Services;
using FinancialTracker.ViewModels;
using Microsoft.Maui.Handlers;
#if WINDOWS
using FinancialTracker.Platforms.Windows;
#endif
#if IOS
using FinancialTracker.Platforms.iOS;
#endif

namespace FinancialTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureMauiHandlers(_ =>
            {
                ViewHandler.ViewMapper.AppendToMapping(
                    nameof(InteractionAnimations),
                    static (_, view) =>
                    {
                        if (view is View element)
                        {
                            InteractionAnimations.AttachPressFeedback(element);
                        }
                    });
                EntryHandler.Mapper.AppendToMapping(
                    nameof(EntryChrome),
                    static (handler, _) => EntryChrome.RemoveNativeBorder(handler));
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<LocalDatabase>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<MonthlyBudgetService>();
        builder.Services.AddSingleton<AssetPortfolioService>();
        builder.Services.AddSingleton<TransactionDataStore>();
        builder.Services.AddSingleton<TransactionStatisticsService>();
        builder.Services.AddSingleton<WidgetSnapshotCoordinator>();
        builder.Services.AddSingleton<AssetWidgetSnapshotCoordinator>();
        builder.Services.AddSingleton<WidgetAppearanceCoordinator>();
#if IOS
        builder.Services.AddSingleton<
            IWidgetSnapshotPublisher,
            IosWidgetSnapshotPublisher>();
        builder.Services.AddSingleton<
            IAssetWidgetSnapshotPublisher,
            IosAssetWidgetSnapshotPublisher>();
        builder.Services.AddSingleton<
            IWidgetAppearancePublisher,
            IosWidgetAppearancePublisher>();
        builder.Services.AddSingleton<
            IWidgetSettingsStore,
            IosWidgetSettingsStore>();
#else
        builder.Services.AddSingleton<
            IWidgetSnapshotPublisher,
            NoOpWidgetSnapshotPublisher>();
        builder.Services.AddSingleton<
            IAssetWidgetSnapshotPublisher,
            NoOpAssetWidgetSnapshotPublisher>();
        builder.Services.AddSingleton<
            IWidgetAppearancePublisher,
            NoOpWidgetAppearancePublisher>();
        builder.Services.AddSingleton<
            IWidgetSettingsStore,
            NoOpWidgetSettingsStore>();
#endif
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<BudgetSettingsViewModel>();
        builder.Services.AddSingleton<TransactionStatisticsViewModel>();
        builder.Services.AddSingleton<TransactionStatisticsDetailViewModel>();
#if WINDOWS
        builder.Services.AddSingleton<IBackupFileSaver, WindowsBackupFileSaver>();
#else
        builder.Services.AddSingleton<IBackupFileSaver, ShareBackupFileSaver>();
#endif
#if IOS
        builder.Services.AddSingleton<IBackupFilePicker, IosBackupFilePicker>();
#else
        builder.Services.AddSingleton<IBackupFilePicker, MauiBackupFilePicker>();
#endif
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
