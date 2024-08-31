namespace Aptabase.Maui;

/// <summary>
/// Aptabase client used for tracking events
/// </summary>
public interface IAptabaseClient : IAsyncDisposable
{
    Task TrackEvent(string eventName, Dictionary<string, object>? props = null);
}

