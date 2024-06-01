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
}
