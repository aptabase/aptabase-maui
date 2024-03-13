namespace Aptabase.Maui;


/// <summary>
/// Initialization options for the Aptabase Client
/// </summary>
public class InitOptions
{
	/// <summary>
	/// Custom host for Self-Hosted instances
	/// </summary>
	public string? Host { get; set; }

	/// <summary>
	/// Manual overide to make the client run in debug mode
	/// </summary>
	public bool IsDebug { get; set; }

}
