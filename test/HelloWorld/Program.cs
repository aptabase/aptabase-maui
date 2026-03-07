using Aptabase.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

services.UseAptabase("A-EU-1687478437", new AptabaseOptions
{
#if DEBUG
    IsDebugMode = true,
#else
    IsDebugMode = false,
#endif
    EnableCrashReporting = false,
    EnablePersistence = true,
});

var serviceProvider = services.BuildServiceProvider();

// Get an instance of the Aptabase service
var aptabaseClient = serviceProvider.GetRequiredService<IAptabaseClient>(); 


Console.WriteLine("Hello, World!");
// Track a sample event
await aptabaseClient.TrackEvent("app_started");

// OR 
Console.Write("What's your favorite color? ");

var favoriteColor = Console.ReadLine()?.Trim().ToLowerInvariant();

if (!string.IsNullOrWhiteSpace(favoriteColor))
{
    await aptabaseClient.TrackEvent("favorite_color", new Dictionary<string, object>
    {
        { "color", favoriteColor }
    });

    Console.WriteLine("Thanks! Your response was logged.");
}
else
{
    Console.WriteLine("No input received.");
}


Console.ReadKey();