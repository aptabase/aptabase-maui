using Microsoft.Extensions.Logging;
using Aptabase.Maui;

namespace HelloWorld;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
            .UseAptabase("A-EU-1687478437", new AptabaseOptions
			{
#if DEBUG
				IsDebugMode = true,
#else
				IsDebugMode = false,
#endif
				EnableCrashReporting = true,
				EnablePersistence = true,
			})
            .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

