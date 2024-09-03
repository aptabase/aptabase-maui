using Microsoft.Extensions.Logging;

namespace Aptabase.Maui;

public class AptabaseCrashReporter
{
    private readonly IAptabaseClient _client;
    private readonly ILogger<AptabaseCrashReporter>? _logger;
    private readonly AptabaseOptions? _options;
    private const int _pauseTimeoutSeconds = 30;

#if ANDROID
    // the UnhandledExceptionRaiser fires first, but others may fire redundantly soon after
    private bool _nativeThrown;
#endif

    public AptabaseCrashReporter(IAptabaseClient client, AptabaseOptions? options, ILogger<AptabaseCrashReporter>? logger)
    {
        _client = client;
        _logger = logger;
        _options = options;

        RegisterUncaughtExceptionHandler();
    }

    public void RegisterUncaughtExceptionHandler()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            TrackError((Exception)e.ExceptionObject, e.IsTerminating ? "ApplicationCrash" : "ApplicationException", DateTime.UtcNow, e.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (sender, ueargs) =>
        {
            var stamp = DateTime.UtcNow;
            foreach (var e in ueargs.Exception.InnerExceptions)
                TrackError(e, "ApplicationTaskException", stamp);
        };

#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            TrackError(args.Exception, "ApplicationCrash", DateTime.UtcNow, true);
            _nativeThrown = true;
        };
#endif

#if IOS || MACCATALYST
        // https://github.com/xamarin/xamarin-macios/issues/15252  
        ObjCRuntime.Runtime.MarshalManagedException += (_, args) =>
        {
            args.ExceptionMode = ObjCRuntime.MarshalManagedExceptionMode.UnwindNativeCode;
        };
#endif
    }

    private void TrackError(Exception e, string error, DateTime timeStamp, bool fatal = false)
    {
#if ANDROID
        if (_nativeThrown) return;
#endif

        string thing = $"{(fatal ? "Fatal " : string.Empty)}{e.GetType().Name}: {e.Message}";
        string stamp = $"{timeStamp:o}";
        int i = 0;

        // include additional useful platform info
        var di = DeviceInfo.Current;
        thing += $" ({di.Platform}{di.VersionString}-{di.Manufacturer}-{di.Idiom}-{di.Model})";

        if (fatal && _options?.EnablePersistence == true)
        {
            _client.StopAsync(); // queue events but don't start sending, to avoid duplicates or errors

            Task.Run(async () =>
            {
                await Task.Delay(_pauseTimeoutSeconds * 1000);
                await _client.StartAsync();
            });
        }

        // event 00 is the exception summary
        _client.TrackEventAsync(error, new Dictionary<string, object> { { stamp, $"{i++:00} {thing}" } });

        // plus any stacktrace, events 01..nn will be sequenced under same stamp
        if (string.IsNullOrEmpty(e.StackTrace))
            return;

        // this simple approach closely mimics the log emitted by mono_rt
        foreach (var f in e.StackTrace.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            // elide noisy separators and runtime frames
            if (!f.StartsWith("---") && !f.Contains(" System.Runtime."))
                _client.TrackEventAsync(error, new Dictionary<string, object> { { stamp, $"{i++:00} {f}" } });
        }

        _logger?.LogError(e, "Tracked error: {ErrorType}", error);
    }
}
