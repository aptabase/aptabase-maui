using DotNext.Threading.Channels;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Aptabase.Maui;

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

    private bool _pauseProcessing;

    public AptabasePersistentClient(string appKey, AptabaseOptions? options, ILogger<AptabasePersistentClient>? logger)
    {
        _client = new AptabaseClientBase(appKey, options, logger);
        _channel = new PersistentEventDataChannel(new PersistentChannelOptions
        {
            SingleReader = true,
            ReliableEnumeration = true,
            PartitionCapacity = _maxPersistedEvents,
            Location = Path.Combine(FileSystem.CacheDirectory, "EventData"),
        });
        _logger = logger;
        _cts = new CancellationTokenSource();
        _processingTask = Task.Run(ProcessEventsAsync);
    }

    public async Task TrackEvent(string eventName, Dictionary<string, object>? props = null)
    {
        var eventData = new EventData(eventName, props);

        if (eventName == "ApplicationCrash")
        {
            // pause ProcessEvents across crash recovery
            _pauseProcessing = true;
        }

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
                        _logger?.LogError("ProcessEvents flushed {Name}@{Timestamp}", eventData.EventName, eventData.Timestamp);
                        
                        continue;
                    }

                    if (eventData.EventName == _invalidPersistedEvent)
                    {
                        _logger?.LogError("ProcessEvents undecodable event");

                        continue;
                    }

                    if (_pauseProcessing)
                    {
                        _pauseProcessing = false;   // will re-send after pause if non-fatal
                        throw new Exception("Paused");
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
        catch { }

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
            var b = new byte[1];
            while (await input.ReadAsync(b, token) > 0 && b[0] != '\n')
            {
                sb.Append((char)b[0]);
            }
            return sb.ToString();
        }
    }
}
