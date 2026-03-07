using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Aptabase.Maui;

public class AptabaseClient : IAptabaseClient
{
    private readonly Channel<EventData> _channel;
    private readonly AptabaseClientBase _client;
    private readonly ILogger<AptabaseClient>? _logger;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _processingTask != null && !_processingTask.IsCompleted;

    public AptabaseClient(string appKey, AptabaseOptions? options, ILogger<AptabaseClient>? logger)
    {
        _client = new AptabaseClientBase(appKey, options, logger);
        _channel = Channel.CreateUnbounded<EventData>();
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
        try
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_channel.Reader.TryRead(out var eventData))
                {

                    try
                    {
                        await _client.TrackEventAsync(eventData, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // best effort
                        _logger?.LogError(ex, "Failed to perform TrackEvent");
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (ChannelClosedException)
        {
            // ignore
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();

        await StopAsync();

        await _client.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}