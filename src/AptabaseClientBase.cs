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

    internal async Task TrackEvent(EventData eventData)
    {
        if (_http is null)
        {
            return;
        }

        RefreshSession();

        eventData.SessionId = _sessionId;
        eventData.SystemProps = _sysInfo;

        var body = JsonContent.Create(eventData);

        var response = await _http.PostAsync("/api/v0/event", body);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode >= HttpStatusCode.InternalServerError ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // throw error, should be retried
                response.EnsureSuccessStatusCode();
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            _logger?.LogError("Failed to perform TrackEvent due to {StatusCode} and response body {Body}", response.StatusCode, responseBody);
        }
    }

    // Identifies the SDK platform server-side; distinct from the OS name.
    private const string PlatformName = ".NET MAUI";

    // Field limits enforced by the host's ErrorBody.IsValid(); exceeding any of these
    // causes the whole error to be rejected with 400, so we truncate before sending.
    private const int MaxErrorMessage = 5000;
    private const int MaxErrorType = 100;
    private const int MaxStackTrace = 10000;
    private const int MaxPlatform = 30;
    private const int MaxOsName = 30;
    private const int MaxOsVersion = 100;
    private const int MaxAppVersion = 50;
    private const int MaxSdkVersion = 40;
    private const int MaxSessionId = 100;

    internal bool IsEnabled => _http is not null;

    // Stamps the error with session/system context and clamps every field to the host's
    // limits. Done at capture time so persisted crashes keep their original context even
    // when delivered on a later launch.
    internal void EnrichError(ErrorData errorData)
    {
        RefreshSession();

        errorData.SessionId = Truncate(_sessionId, MaxSessionId);
        errorData.Platform = Truncate(PlatformName, MaxPlatform);
        errorData.OsName = Truncate(_sysInfo.OsName, MaxOsName);
        errorData.OsVersion = Truncate(_sysInfo.OsVersion, MaxOsVersion);
        errorData.AppVersion = Truncate(_sysInfo.AppVersion, MaxAppVersion);
        errorData.SdkVersion = Truncate(_sysInfo.SdkVersion, MaxSdkVersion);
        errorData.IsDebug = _sysInfo.IsDebug;

        errorData.ErrorMessage = Truncate(errorData.ErrorMessage, MaxErrorMessage)!;
        errorData.ErrorType = Truncate(errorData.ErrorType, MaxErrorType)!;
        errorData.StackTrace = Truncate(errorData.StackTrace, MaxStackTrace);
    }

    internal async Task SendErrorAsync(ErrorData errorData)
    {
        if (_http is null)
        {
            return;
        }

        var body = JsonContent.Create(errorData);

        var response = await _http.PostAsync("/api/v0/error", body);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode >= HttpStatusCode.InternalServerError ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // throw error, should be retried
                response.EnsureSuccessStatusCode();
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            _logger?.LogError("Failed to perform TrackError due to {StatusCode} and response body {Body}", response.StatusCode, responseBody);
        }
    }

    // Convenience path for the in-memory client: enrich and send in one step.
    internal async Task TrackError(ErrorData errorData)
    {
        if (_http is null)
        {
            return;
        }

        EnrichError(errorData);
        await SendErrorAsync(errorData);
    }

    private static string? Truncate(string? value, int maxLength)
        => value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;

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
