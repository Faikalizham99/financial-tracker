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
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<BudgetSettingsViewModel>();
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
