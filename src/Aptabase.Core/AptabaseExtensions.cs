
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aptabase.Core;


public static class AptabaseExtensions
{
    public static IServiceCollection UseAptabase(this IServiceCollection services, string appKey, AptabaseOptions? options = null)
    {
        services.AddSingleton<IAptabaseClient>(serviceProvider =>
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
                throw new NotImplementedException("Crash reporting is only for Aptabase.Maui");
            }

            return client;
        });

        return services;
    }


}