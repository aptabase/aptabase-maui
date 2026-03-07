using Aptabase.Maui;
using Microsoft.Extensions.Logging;

namespace Microsoft.Maui.Hosting;

/// <summary>
/// Aptabase extensions for <see cref="MauiAppBuilder"/>.
/// </summary>
public static class AptabaseExtensions
{
    /// <summary>
    /// Uses Aptabase integration.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="appKey">The App Key.</param>
    /// <param name="options">Initialization Options.</param>
    /// <returns>The <paramref name="builder"/>.</returns>
    public static MauiAppBuilder UseAptabase(this MauiAppBuilder builder, string appKey, AptabaseOptions? options = null)
    {
        builder.Services.AddSingleton(serviceProvider =>
        {
            IAptabaseClient client;
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            if (options?.EnablePersistence != true)
            {
                client = new AptabaseClient(appKey, options, loggerFactory.CreateLogger<AptabaseClient>());
            }
            else
            {
                client = new AptabasePersistentClient(appKey, options, loggerFactory.CreateLogger<AptabasePersistentClient>());
            }

            if (options?.EnableCrashReporting == true)
            {
                _ = new AptabaseCrashReporter(client, options, loggerFactory.CreateLogger<AptabaseCrashReporter>());
            }

            return client;
        });

        return builder;
    }
}


