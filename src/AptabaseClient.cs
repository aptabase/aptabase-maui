using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Channels;

// https://dotnet.github.io/dotNext/features/threading/channel.html
using DotNext.Threading.Channels;
using System.Text;
using System.Text.Json;
using System.Net;

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
    private static readonly SystemInfo _sysInfo = new();
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
        { "DEV",  DeviceInfo.Platform == DevicePlatform.Android ? "https://10.0.2.2:3000" : "https://localhost:3000" },
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

        if (options?.IsPersistent == true)
        {
            _pchannel = new PersistentEventDataChannel(new PersistentChannelOptions()
            {
                SingleReader = true,
                ReliableEnumeration = true,
                Location = Path.Combine(FileSystem.CacheDirectory, "EventData"),
            });

            _processingTask = Task.Run(ProcessPersistentEventsAsync);
        }
        else
        {
            _channel = Channel.CreateUnbounded<EventData>();

            _processingTask = Task.Run(ProcessEventsAsync);
        }
    }

    /// <summary>
    /// Sends a telemetry event to Aptabase
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="props">A list of key/value pairs.</param>
    public void TrackEvent(string eventName, Dictionary<string, object>? props = null)
    {
        if (_pchannel is not null)
        {
            TrackPersistentEvent(eventName, props);
            return;
        }

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
                if (eventData is not null)
                {
                    eventData.SessionId = _sessionId;
                    eventData.SystemProps = _sysInfo;
                }

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
            var body = JsonContent.Create(eventData);

            var response = await _http.PostAsync("/api/v0/event", body);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode >= HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    throw new TaskCanceledException($"Server returned {response.StatusCode}");
                }

                var responseBody = await response.Content.ReadAsStringAsync();

                _logger?.LogError("Failed to perform TrackEvent due to {StatusCode} and response body {Body}", response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            if (_pchannel is not null) // retryable (later)
            {
                throw;
            }
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
        _pchannel?.Writer.Complete();

        if (_processingTask?.IsCompleted == false)
        {
            await _processingTask;
        }

        _http?.Dispose();

        GC.SuppressFinalize(this);
    }

    // Persistent event channel support

    private static int _maxPersistedEvents = 1000;
    private const string _invalidPersistedEvent = "%%%DELETE%%%";
    private readonly PersistentEventDataChannel? _pchannel;

    private sealed class PersistentEventDataChannel : PersistentChannel<EventData, EventData>
    {
        internal PersistentEventDataChannel(PersistentChannelOptions options) : base(options)
        {
            if (options?.PartitionCapacity > 0)
                _maxPersistedEvents = options.PartitionCapacity;
        }

        protected override async ValueTask<EventData> DeserializeAsync(Stream input, CancellationToken token)
        {
            try
            {
                return JsonSerializer.Deserialize(await ExtractJsonObject(input, token), typeof(EventData)) as EventData ?? throw new NullReferenceException();
            }
            catch
            {
                // NOTE must not throw any deserialization failure or ReliableReader.MoveNextAsync() will never consume the event!
                return new EventData(_invalidPersistedEvent);
            }
        }

        protected override ValueTask SerializeAsync(EventData input, Stream output, CancellationToken token)
        {
            JsonSerializer.Serialize(output, input);
            output.WriteByte((byte)'\n');   // append jsonl/ndjson separator
            output.Flush();
            return new ValueTask();
        }

        private async static Task<string> ExtractJsonObject(Stream input, CancellationToken token)
        {
            StringBuilder sb = new();
            byte[] b = new byte[1];
            while (await input.ReadAsync(b, token) > 0 && b[0] != '\n')
                sb.Append((char)b[0]);
            return sb.ToString();
        }
    }

    private async void TrackPersistentEvent(string eventName, Dictionary<string, object>? props = null)
    {
        if (_pchannel is null)
        {
            return;
        }

        RefreshSession();

        var eventData = new EventData(eventName, props) { SessionId = _sessionId, SystemProps = _sysInfo };

        try
        {
            await _pchannel.Writer.WriteAsync(eventData);    // NOTE TryWrite not supported on PersistentChannelWriter
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform TrackEvent");
        }
    }

    private async ValueTask ProcessPersistentEventsAsync()
    {
        if (_pchannel is null)
        {
            return;
        }

        if (_http is null)
        {
            return;
        }

        while (true)
        {
            try
            {
                await foreach (EventData eventData in _pchannel.Reader.ReadAllAsync())
                {
                    if (_pchannel.RemainingCount > _maxPersistedEvents)
                    {
                        _logger?.LogError("ProcessEvents flushed {Name}@{Timestamp}", eventData.EventName, eventData.Timestamp);
                        continue;
                    }
                    if (eventData.EventName == _invalidPersistedEvent)
                    {
                        _logger?.LogError("ProcessEvents undecodable event");
                        continue;
                    }
                    await SendEventAsync(eventData);
                }
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Wait an extra HttpClient timeout, default 100s
                _logger?.LogInformation(ex, "ProcessEvents retrying in {Seconds}s", _http.Timeout.TotalSeconds);
                await Task.Delay((int)_http.Timeout.TotalMilliseconds);
            }
        }
    }
}