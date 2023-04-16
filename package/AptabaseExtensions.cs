using Aptabase.Maui;

namespace Microsoft.Maui.Hosting;

public static class AptabaseExtensions
{
    public static MauiAppBuilder UseAptabase(this MauiAppBuilder builder, string appKey)
    {
        var client = new AptabaseClient(appKey);
        builder.Services.AddSingleton<IAptabaseClient>(client);
        return builder;
    }
}


