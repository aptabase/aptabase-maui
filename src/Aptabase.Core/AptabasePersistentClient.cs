using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Xml;
using DotNext.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Aptabase.Core;

[JsonSerializable(typeof(EventData))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonDocument))]
[JsonSerializable(typeof(XmlNode))]
[JsonSerializable(typeof(XmlElement))]
[JsonSerializable(typeof(XmlDocument))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<EventData>))]
[JsonSerializable(typeof(List<KeyValuePair<string, string>>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(Int128))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(ushort))]
internal partial class AptabaseContext : JsonSerializerContext;

public class AptabasePersistentClient : IAptabaseClient
{
    private const int _maxPersistedEvents = 1000;
    private const string _invalidPersistedEvent = "%%%DELETE%%%";
    private const int _retrySeconds = 30;

    private readonly PersistentEventDataChannel _channel;
    private readonly Task? _processingTask;
    private readonly AptabaseClientBase _client;
    private readonly ILogger<AptabasePersistentClient>? _logger;
    private readonly CancellationTokenSource _cts;

    public AptabasePersistentClient(string appKey, AptabaseOptions? options, ILogger<AptabasePersistentClient>? logger)
    {
        _client = new AptabaseClientBase(appKey, options, logger);
        _channel = new PersistentEventDataChannel(new PersistentChannelOptions
        {
            SingleReader = true,
            ReliableEnumeration = true,
            PartitionCapacity = _maxPersistedEvents,
            Location = Path.Combine(CacheHome, "Aptabase", "EventData"),
        });
        _logger = logger;
        _cts = new CancellationTokenSource();
        _processingTask = Task.Run(ProcessEventsAsync);
    }

    private static string CacheHome =>
        Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
        ?? GetCurrentPlatform() switch
        {
            var platform when platform == OSPlatform.Windows
                => Environment.GetEnvironmentVariable("LOCALAPPDATA") is not null
                    ? Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "cache")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "cache"),
            var platform when platform == OSPlatform.OSX
                => Path.Combine(Home, "Library", "Caches"),
            _ => Path.Combine(Home, ".cache") // Linux/FreeBSD
        };

    private static OSPlatform? GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSPlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSPlatform.OSX;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OSPlatform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) return OSPlatform.FreeBSD;

        return null;
    }

    private static string Home
    {
        get
        {
            var homeEnv = GetCurrentPlatform() switch
            {
                var platform when platform == OSPlatform.Windows => Environment.GetEnvironmentVariable("USERPROFILE") ??
                                                                    Environment.GetFolderPath(Environment.SpecialFolder
                                                                        .UserProfile),
                _ => Environment.GetEnvironmentVariable("HOME") // Unix*
            };
            return homeEnv ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
    }

    public async Task TrackEvent(string eventName, Dictionary<string, object>? props = null)
    {
        var eventData = new EventData(eventName, props);

        try
        {
            await _channel.Writer.WriteAsync(eventData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform TrackEvent");
        }
    }

    private async ValueTask ProcessEventsAsync()
    {
        while (true)
        {
            if (_cts.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await foreach (EventData eventData in _channel.Reader.ReadAllAsync())
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }

                    if (_channel.RemainingCount > _maxPersistedEvents)
                    {
                        _logger?.LogError("ProcessEvents flushed {Name}@{Timestamp}", eventData.EventName,
                            eventData.Timestamp);

                        continue;
                    }

                    if (eventData.EventName == _invalidPersistedEvent)
                    {
                        _logger?.LogError("ProcessEvents undecodable event");

                        continue;
                    }

                    await _client.TrackEvent(eventData);
                }
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, "ProcessEvents retrying in {Seconds}s", _retrySeconds);

                await Task.Delay(_retrySeconds * 1000);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts.Cancel();
        }
        catch
        {
        }

        _channel.Writer.Complete();

        if (_processingTask?.IsCompleted == false)
        {
            await _processingTask;
        }

        _cts.Dispose();

        await _client.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    private sealed class PersistentEventDataChannel : PersistentChannel<EventData, EventData>
    {
        internal PersistentEventDataChannel(PersistentChannelOptions options) : base(options)
        {
        }

        protected override async ValueTask<EventData> DeserializeAsync(Stream input, CancellationToken token)
        {
            try
            {
                return JsonSerializer.Deserialize(await ExtractJsonObject(input, token),
                    AptabaseContext.Default.EventData) ?? throw new NullReferenceException();
            }
            catch
            {
                // NOTE must not throw any deserialization failure or ReliableReader.MoveNextAsync() will never consume the event!
                return new EventData(_invalidPersistedEvent);
            }
        }

        protected override ValueTask SerializeAsync(EventData input, Stream output, CancellationToken token)
        {
            JsonSerializer.Serialize(output, input, AptabaseContext.Default.EventData);
            output.WriteByte((byte)'\n'); // append jsonl/ndjson separator
            output.Flush();
            return new ValueTask();
        }

        private async static Task<string> ExtractJsonObject(Stream input, CancellationToken token)
        {
            StringBuilder sb = new();
            var b = new byte[1];
            while (await input.ReadAsync(b, token) > 0 && b[0] != '\n')
            {
                sb.Append((char)b[0]);
            }

            return sb.ToString();
        }
    }
}