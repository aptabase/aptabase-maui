namespace Aptabase.Maui;

/// <summary>
/// Initialization options for the Aptabase Client
/// </summary>
public class AptabaseOptions
{
    /// <summary>
    /// Specifies the custom host URL for Self-Hosted instances of the Aptabase. This setting is required for Self-Hosted instances.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Indicates whether the client should operate in Debug mode. This setting is optional and helps in enabling debug-specific functionalities or logging.
    /// </summary>
    public bool? IsDebugMode { get; set; }

    /// <summary>
    /// Indicates whether the client should provide a reliable, persistent channel. This setting is optional and may be useful for crash reporting.
    /// </summary>
    public bool? EnablePersistence { get; set; }

    /// <summary>
    /// Indicates whether the client should provide simple crash reporting. This setting is optional and the use of a persistent channel is recommended.
    /// When set, any uncaught exception is timestamped and logged with a TrackEvent. If stacktrace frames are provided by the exception, an additional TrackEvent is logged for each frame.
    /// </summary>
    public bool? EnableCrashReporting { get; set; }
}
