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
    private readonly AptabaseClientBase _client;
    private readonly ILogger<AptabasePersistentClient>? _logger;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _processingTask != null && !_processingTask.IsCompleted;

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

        StartAsync();
    }

    public Task StartAsync()
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _processingTask = ProcessEventsAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return; 
        }

        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        try
        {
            if (_processingTask != null)
            {
                await _processingTask;
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _processingTask = null;
        }
    }

    public async Task TrackEventAsync(string eventName, Dictionary<string, object>? props = null, CancellationToken cancellationToken = default)
    {
        var eventData = new EventData(eventName, props);

        try
        {
            await _channel.Writer.WriteAsync(eventData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform TrackEvent");
        }
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (EventData eventData in _channel.Reader.ReadAllAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

                    await _client.TrackEventAsync(eventData, cancellationToken);
                }
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, "ProcessEvents retrying in {Seconds}s", _retrySeconds);

                await Task.Delay(_retrySeconds * 1000, cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();

        await StopAsync();

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
