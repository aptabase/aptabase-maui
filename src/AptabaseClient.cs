using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Channels;

namespace Aptabase.Maui;

/// <summary>
/// Aptabase client used for tracking events
/// </summary>
public interface IAptabaseClient
{
	void TrackEvent(string eventName, Dictionary<string, object>? props = null);
}

/// <summary>
/// Aptabase client used for tracking events
/// </summary>
public class AptabaseClient : IAptabaseClient, IAsyncDisposable
{
    private static readonly Random _random = new();
    private static readonly SystemInfo _sysInfo = new(Assembly.GetExecutingAssembly());
    private static readonly TimeSpan SESSION_TIMEOUT = TimeSpan.FromMinutes(60);

    private readonly ILogger<AptabaseClient>? _logger;
    private readonly HttpClient? _http;
    private DateTime _lastTouched = DateTime.UtcNow;
    private string _sessionId = NewSessionId();

    private readonly Channel<EventData>? _channel;
    private readonly Task? _processingTask;

    private static readonly Dictionary<string, string> _hosts = new()
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
    public AptabaseClient(string appKey, AptabaseOptions? options, ILogger<AptabaseClient>? logger)
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
        {
            return;
        }

        _http = new()
        {
            BaseAddress = new Uri(baseUrl)
        };

        _http.DefaultRequestHeaders.Add("App-Key", appKey);

        _channel = Channel.CreateUnbounded<EventData>();

        _processingTask = Task.Run(ProcessEventsAsync);
    }

    /// <summary>
    /// Sends a telemetry event to Aptabase
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="props">A list of key/value pairs.</param>
    public void TrackEvent(string eventName, Dictionary<string, object>? props = null)
    {
        if (_channel is null)
        {
            return;
        }

        if (!_channel.Writer.TryWrite(new EventData(eventName, props)))
        {
            _logger?.LogError("Failed to perform TrackEvent");
        }
    }

    private async ValueTask ProcessEventsAsync()
    {
        if (_channel is null)
        {
            return;
        }

        while (await _channel.Reader.WaitToReadAsync())
        {
            RefreshSession();

            while (_channel.Reader.TryRead(out EventData? eventData))
            {
                await SendEventAsync(eventData);
            }
        }
    }

    private async Task SendEventAsync(EventData? eventData)
    {
        if (eventData is null)
        {
            return;
        }

        if (_http is null)
        {
            return;
        }

        try
        {
            eventData.SessionId = _sessionId;
            eventData.SystemProps = _sysInfo;

            var body = JsonContent.Create(eventData);

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

    public async ValueTask DisposeAsync()
    {
        _channel?.Writer.Complete();

        if (_processingTask?.IsCompleted == false)
        {
            await _processingTask;
        }

        _http?.Dispose();  

        GC.SuppressFinalize(this);
    }
}