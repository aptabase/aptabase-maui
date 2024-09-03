using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;

namespace Aptabase.Maui;

internal class AptabaseClientBase : IAsyncDisposable
{
    protected static readonly TimeSpan SESSION_TIMEOUT = TimeSpan.FromMinutes(60);

    private static readonly Random _random = new();
    private readonly ILogger? _logger;
    private readonly HttpClient? _http;
    private DateTime _lastTouched = DateTime.UtcNow;
    private string _sessionId = NewSessionId();
    private static readonly SystemInfo _sysInfo = new();

    private static readonly Dictionary<string, string> _hosts = new()
    {
        { "US", "https://us.aptabase.com" },
        { "EU", "https://eu.aptabase.com" },
        { "DEV", DeviceInfo.Platform == DevicePlatform.Android ? "https://10.0.2.2:3000" : "https://localhost:3000" },
        { "SH", "" },
    };

    public AptabaseClientBase(string appKey, AptabaseOptions? options, ILogger? logger)
    {
        _logger = logger;

        var parts = appKey.Split("-");

        if (parts.Length != 3 || !_hosts.ContainsKey(parts[1]))
        {
            _logger?.LogWarning("The Aptabase App Key {AppKey} is invalid. Tracking will be disabled.", appKey);
            return;
        }

        var region = parts[1];

        var baseUrl = GetBaseUrl(parts[1], options);

        if (baseUrl is null)
        {
            return;
        }

        _sysInfo.IsDebug = options?.IsDebugMode ?? SystemInfo.IsInDebugMode(Assembly.GetExecutingAssembly());

        _http = region == "DEV" ? new(new LocalHttpsClientHandler()) : new();
        _http.BaseAddress = new Uri(baseUrl);

        _http.DefaultRequestHeaders.Add("App-Key", appKey);
    }

    internal async Task TrackEventAsync(EventData eventData, CancellationToken cancellationToken)
    {
        if (_http is null)
        {
            return;
        }

        RefreshSession();

        eventData.SessionId = _sessionId;
        eventData.SystemProps = _sysInfo;

        var body = JsonContent.Create(eventData);

        var response = await _http.PostAsync("/api/v0/event", body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode >= HttpStatusCode.InternalServerError ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // throw error, should be retried
                response.EnsureSuccessStatusCode();
            }

            var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);

            _logger?.LogError("Failed to perform TrackEvent due to {StatusCode} and response body {Body}", response.StatusCode, responseBody);
        }
    }

    public virtual ValueTask DisposeAsync()
    {
        _http?.Dispose();

        return ValueTask.CompletedTask;
    }

    private void RefreshSession()
    {
        var now = DateTime.UtcNow;
        var timeSince = now.Subtract(_lastTouched);

        if (timeSince >= SESSION_TIMEOUT)
        {
            _sessionId = NewSessionId();
        }

        _lastTouched = now;
    }

    private static string NewSessionId()
    {
        var epochInSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var random = _random.NextInt64(0, 99999999);

        return (epochInSeconds * 100000000 + random).ToString();
    }

    private string? GetBaseUrl(string region, AptabaseOptions? options)
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
}
