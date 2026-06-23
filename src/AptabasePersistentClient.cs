using DotNext.Threading.Channels;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Aptabase.Maui;

public class AptabasePersistentClient : IAptabaseClient, IErrorTracker
{
    private const int _maxPersistedEvents = 1000;
    private const string _invalidPersistedEvent = "%%%DELETE%%%";
    private const string _invalidPersistedError = "%%%DELETE%%%";
    private const int _retrySeconds = 30;

    private readonly PersistentEventDataChannel _channel;
    private readonly PersistentErrorDataChannel _errorChannel;
    private readonly Task? _processingTask;
    private readonly Task? _errorProcessingTask;
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
            Location = Path.Combine(FileSystem.CacheDirectory, "EventData"),
        });
        _errorChannel = new PersistentErrorDataChannel(new PersistentChannelOptions
        {
            SingleReader = true,
            ReliableEnumeration = true,
            PartitionCapacity = _maxPersistedEvents,
            Location = Path.Combine(FileSystem.CacheDirectory, "ErrorData"),
        });
        _logger = logger;
        _cts = new CancellationTokenSource();
        _processingTask = Task.Run(ProcessEventsAsync);
        _errorProcessingTask = Task.Run(ProcessErrorsAsync);
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

    public Task TrackError(Exception exception, bool fatal = false)
        => ((IErrorTracker)this).TrackError(exception, fatal, fatal ? "crash" : "handled");

    async Task IErrorTracker.TrackError(Exception exception, bool fatal, string kind)
    {
        if (!_client.IsEnabled)
        {
            return;
        }

        var errorData = ErrorData.FromException(exception, fatal, kind);

        // Enrich (session/system context + truncation) at capture time, then persist to disk.
        // A fatal crash is durably stored even if the process dies before it can be sent;
        // ProcessErrorsAsync delivers it on this or the next launch.
        _client.EnrichError(errorData);

        try
        {
            await _errorChannel.Writer.WriteAsync(errorData);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform TrackError");
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

    private async ValueTask ProcessErrorsAsync()
    {
        while (true)
        {
            if (_cts.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await foreach (ErrorData errorData in _errorChannel.Reader.ReadAllAsync())
                {
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }

                    if (errorData.ErrorType == _invalidPersistedError)
                    {
                        _logger?.LogError("ProcessErrors undecodable error");

                        continue;
                    }

                    // Already enriched and truncated at capture time; just deliver it.
                    await _client.SendErrorAsync(errorData);
                }
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, "ProcessErrors retrying in {Seconds}s", _retrySeconds);

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
        _errorChannel.Writer.Complete();

        if (_processingTask?.IsCompleted == false)
        {
            await _processingTask;
        }

        if (_errorProcessingTask?.IsCompleted == false)
        {
            await _errorProcessingTask;
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

    private sealed class PersistentErrorDataChannel : PersistentChannel<ErrorData, ErrorData>
    {
        internal PersistentErrorDataChannel(PersistentChannelOptions options) : base(options)
        {
        }

        protected override async ValueTask<ErrorData> DeserializeAsync(Stream input, CancellationToken token)
        {
            try
            {
                return JsonSerializer.Deserialize(await ExtractJsonObject(input, token), typeof(ErrorData)) as ErrorData ?? throw new NullReferenceException();
            }
            catch
            {
                // NOTE must not throw any deserialization failure or ReliableReader.MoveNextAsync() will never consume the error!
                return new ErrorData("invalid", _invalidPersistedError);
            }
        }

        protected override ValueTask SerializeAsync(ErrorData input, Stream output, CancellationToken token)
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
