using Aptabase.Maui;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

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
    /// <returns>The <paramref name="builder"/>.</returns>
    public static MauiAppBuilder UseAptabase(this MauiAppBuilder builder, string appKey, AptabaseOptions? options = null)
    {
        builder.Services.AddSingleton<IAptabaseClient>(x =>
        {
            var logger = x.GetService<ILogger<AptabaseClient>>();
            return new AptabaseClient(appKey, options, logger);
        });

        return builder;
    }
}


