namespace Aptabase.Maui;

/// <summary>
/// Aptabase client used for tracking events
/// </summary>
public interface IAptabaseClient : IAsyncDisposable
{
    bool IsRunning { get; }
    Task StartAsync();
    Task StopAsync();
    Task TrackEventAsync(string eventName, Dictionary<string, object>? props = null, CancellationToken cancellationToken = default);
}

