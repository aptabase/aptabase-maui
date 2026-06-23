using Microsoft.Extensions.Logging;

namespace Aptabase.Maui;

public class AptabaseCrashReporter
{
    private readonly IAptabaseClient _client;
    private readonly ILogger<AptabaseCrashReporter>? _logger;

#if ANDROID
    // the UnhandledExceptionRaiser fires first, but others may fire redundantly soon after
    private bool _nativeThrown;
#endif

    public AptabaseCrashReporter(IAptabaseClient client, ILogger<AptabaseCrashReporter>? logger)
    {
        _client = client;
        _logger = logger;

        RegisterUncaughtExceptionHandler();
    }

    public void RegisterUncaughtExceptionHandler()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            TrackError((Exception)e.ExceptionObject, e.IsTerminating ? "crash" : "unhandled", e.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (sender, ueargs) =>
        {
            foreach (var e in ueargs.Exception.InnerExceptions)
                TrackError(e, "taskException");
        };

#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            TrackError(args.Exception, "crash", true);
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

    // How long to block a terminating crash so the error can be persisted/sent
    // before the runtime tears the process down.
    private static readonly TimeSpan FatalFlushTimeout = TimeSpan.FromSeconds(3);

    private void TrackError(Exception e, string kind, bool fatal = false)
    {
#if ANDROID
        if (_nativeThrown) return;
#endif

        // Use the richer internal path when available so the error source ("crash",
        // "unhandled", "taskException") is preserved; fall back to the public API otherwise.
        var sendTask = _client is IErrorTracker tracker
            ? tracker.TrackError(e, fatal, kind)
            : _client.TrackError(e, fatal);

        if (fatal)
        {
            // The process is terminating: block (best effort) so the persistent client
            // can flush to disk / the in-memory client can complete the POST.
            try { sendTask.Wait(FatalFlushTimeout); }
            catch { /* best effort during teardown */ }
        }

        _logger?.LogError(e, "Tracked error: {Kind}", kind);
    }
}
