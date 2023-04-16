using Aptabase.Maui;
using Microsoft.Extensions.Logging;

namespace Microsoft.Maui.Hosting;

public static class AptabaseExtensions
{
    public static MauiAppBuilder UseAptabase(this MauiAppBuilder builder, string appKey)
    {
        builder.Services.AddSingleton<IAptabaseClient>(x =>
        {
            var logger = x.GetService<ILogger<AptabaseClient>>();
            return new AptabaseClient(appKey, logger);
        });

        return builder;
    }
}


