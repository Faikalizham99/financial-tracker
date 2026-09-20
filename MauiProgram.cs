using Microsoft.Extensions.Logging;

using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Services;
using FinancialTracker.ViewModels;
using Microsoft.Maui.Handlers;

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
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
