using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Aptabase.Maui;

/// <summary>
/// Aptabase client used for tracking events
/// </summary>
public interface IAptabaseClient
{
	void TrackEvent(string eventName);
	void TrackEvent(string eventName, Dictionary<string, object> props);
}

/// <summary>
/// Aptabase client used for tracking events
/// </summary>
public class AptabaseClient : IAptabaseClient
{
    private static TimeSpan SESSION_TIMEOUT = TimeSpan.FromMinutes(60);
    private static SystemInfo _sysInfo = new(Assembly.GetEntryAssembly());

    private readonly ILogger<AptabaseClient>? _logger;
    private readonly HttpClient? _http;
    private DateTime _lastTouched = DateTime.UtcNow;
    private string _sessionId = NewSessionId();

    private static Dictionary<string, string> _hosts = new()
    {
        { "US", "https://us.aptabase.com" },
        { "EU", "https://eu.aptabase.com" },
        { "DEV", "http://localhost:3000" },
        { "SH", "" },
    };

    /// <summary>
    /// Initializes a new Aptabase Client
    /// </summary>
    /// <param name="appKey">The App Key.</param>
    /// <param name="options">Initialization Options.</param>
    /// <param name="logger">A logger instance.</param>
    public AptabaseClient(string appKey, InitOptions? options, ILogger<AptabaseClient>? logger)
    {
        _logger = logger;

        var parts = appKey.Split("-");
        if (parts.Length != 3 || !_hosts.ContainsKey(parts[1]))
        {
            _logger?.LogWarning("The Aptabase App Key {AppKey} is invalid. Tracking will be disabled.", appKey);
            return;
        }

        var baseUrl = GetBaseUrl(parts[1], options);
        if (baseUrl is null)
            return;

        _http = new();
        _http.BaseAddress = new Uri(baseUrl);
		_http.DefaultRequestHeaders.Add("App-Key", appKey);
    }

    private string? GetBaseUrl(string region, InitOptions? options)
    {
        if (region == "SH")
        {
            if (string.IsNullOrEmpty(options?.Host))
            {
                _logger?.LogWarning("Host parameter must be defined when using Self-Hosted App Key. Tracking will be disabled.");
                return null;
            }

            return options.Host;
        }

        return _hosts[region];
    }


    /// <summary>
    /// Sends a telemetry event to Aptabase
    /// </summary>
    /// <param name="eventName">The event name.</param>
    public void TrackEvent(string eventName)
    {
		this.TrackEvent(eventName, null);
    }

    /// <summary>
    /// Sends a telemetry event to Aptabase
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="props">A list of key/value pairs.</param>
    public void TrackEvent(string eventName, Dictionary<string, object>? props)
    {
        Task.Run(() => SendEvent(eventName, props));
    }

    private async Task SendEvent(string eventName, Dictionary<string, object>? props)
    {
        if (_http is null) return;

        try
        {
            var now = DateTime.UtcNow;
            var timeSince = now.Subtract(_lastTouched);
            if (timeSince >= SESSION_TIMEOUT)
                _sessionId = NewSessionId();

            _lastTouched = now;

            var body = JsonContent.Create(new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                sessionId = _sessionId,
                eventName,
                systemProps = new
                {
                    isDebug = _sysInfo.IsDebug,
                    osName = _sysInfo.OsName,
                    osVersion = _sysInfo.OsVersion,
                    locale = _sysInfo.Locale,
                    appVersion = _sysInfo.AppVersion,
                    appBuildNumber = _sysInfo.AppBuildNumber,
                    sdkVersion = _sysInfo.SdkVersion,
                },
                props
            });

			var response = await _http.PostAsync("/api/v0/event", body);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger?.LogError("Failed to perform TrackEvent due to {StatusCode} and response body {Body}", response.StatusCode, responseBody);
            }
        }
		catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform TrackEvent");
        }
    }

    private static string NewSessionId() => Guid.NewGuid().ToString().ToLower();
}

