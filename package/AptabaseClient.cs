using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Aptabase.Maui;

public interface IAptabaseClient
{
	void TrackEvent(string eventName);
	void TrackEvent(string eventName, Dictionary<string, object> props);
}

public class AptabaseClient : IAptabaseClient
{
    private static TimeSpan SESSION_TIMEOUT = TimeSpan.FromMinutes(60);
    private static SystemInfo _sysInfo = new();

    private readonly ILogger<AptabaseClient> _logger;
    private readonly HttpClient _http;
    private DateTime _lastTouched = DateTime.UtcNow;
    private string _sessionId = NewSessionId();

    private static Dictionary<string, string> _regions = new()
    {
        { "US", "https://api-us.aptabase.com" },
        { "EU", "https://api-eu.aptabase.com" },
        { "DEV", "http://localhost:5251" },
    };

    public AptabaseClient(string appKey, ILogger<AptabaseClient> logger)
	{
        _logger = logger;

        var parts = appKey.Split("-");
        if (parts.Length != 3)
        {
            _logger.LogWarning("The Aptabase App Key {AppKey} is invalid. Tracking will be disabled.", appKey);
            return;
        }

        var region = parts[1];
        var baseURL = _regions.TryGetValue(region, out string value) ? value : _regions["DEV"];

        _http = new();
        _http.BaseAddress = new Uri(baseURL);
		_http.DefaultRequestHeaders.Add("App-Key", appKey);
    }

    public void TrackEvent(string eventName)
    {
		this.TrackEvent(eventName, null);
    }

    public void TrackEvent(string eventName, Dictionary<string, object> props)
    {
        Task.Run(() => SendEvent(eventName, props));
    }

    private async Task SendEvent(string eventName, Dictionary<string, object> props)
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
                    osName = _sysInfo.OsName,
                    osVersion = _sysInfo.OsVersion,
                    locale = _sysInfo.Locale,
                    appVersion = _sysInfo.AppVersion,
                    appBuildNumber = _sysInfo.AppBuildNumber,
                    sdkVersion = _sysInfo.SdkVersion,
                },
                props
            });

			var response = await _http.PostAsync("/v0/event", body);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to perform TrackEvent due to {StatusCode} and response body {Body}", response.StatusCode, responseBody);
            }
        }
		catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform TrackEvent");
        }
    }

    private static string NewSessionId() => Guid.NewGuid().ToString().ToLower();
}

