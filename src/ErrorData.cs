using System.Text.Json.Serialization;

namespace Aptabase.Maui;

internal class ErrorData
{
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; }

    [JsonPropertyName("errorType")]
    public string ErrorType { get; set; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("osName")]
    public string? OsName { get; set; }

    [JsonPropertyName("osVersion")]
    public string? OsVersion { get; set; }

    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; set; }

    [JsonPropertyName("sdkVersion")]
    public string? SdkVersion { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    // Severity of the error: "fatal" for crashes that terminate the process, otherwise "error".
    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    // Source of the error: "crash", "unhandled", "taskException" (from the crash reporter) or "handled" (manual).
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    // Whether the app was built in Debug mode, so the server can keep debug-build errors
    // separate from production data (mirrors the isDebug flag sent with analytics events).
    [JsonPropertyName("isDebug")]
    public bool IsDebug { get; set; }

    public ErrorData(string errorMessage, string errorType, string? stackTrace = null)
    {
        ErrorMessage = errorMessage;
        ErrorType = errorType;
        StackTrace = stackTrace;
        Timestamp = DateTime.UtcNow.ToString("o");
    }

    // Builds an ErrorData from an exception, stamping the time at capture so persisted
    // crashes keep their original timestamp even when delivered on a later launch.
    public static ErrorData FromException(Exception exception, bool fatal, string kind)
    {
        var prefix = fatal ? "Fatal " : "";

        return new ErrorData(
            $"{prefix}{exception.GetType().Name}: {exception.Message}",
            exception.GetType().Name,
            exception.StackTrace)
        {
            Severity = fatal ? "fatal" : "error",
            Kind = kind,
        };
    }
}
