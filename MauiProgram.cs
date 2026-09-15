using Microsoft.Extensions.Logging;

using FinancialTracker.Helpers;
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
                EntryHandler.Mapper.AppendToMapping(
                    nameof(EntryChrome),
                    static (handler, _) => EntryChrome.RemoveNativeBorder(handler));
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
