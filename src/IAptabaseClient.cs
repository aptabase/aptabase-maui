namespace Aptabase.Maui;

/// <summary>
/// Aptabase client used for tracking events and errors
/// </summary>
public interface IAptabaseClient : IAsyncDisposable
{
    Task TrackEvent(string eventName, Dictionary<string, object>? props = null);

    Task TrackError(Exception exception, bool fatal = false);
}

/// <summary>
/// Internal richer entry point that lets the crash reporter convey the error source
/// ("crash", "unhandled", "taskException") without widening the public surface.
/// Returns the in-flight Task so callers can await delivery for fatal crashes.
/// </summary>
internal interface IErrorTracker
{
    Task TrackError(Exception exception, bool fatal, string kind);
}

