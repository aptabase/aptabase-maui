using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;

namespace Aptabase.Maui;

public interface IAptabaseClient
{
	void TrackEvent(string eventName);
	void TrackEvent(string eventName, Dictionary<string, object> props);
}

public class AptabaseClient : IAptabaseClient
{
	private static HttpClient _http = new();
    private static SystemInfo _sysInfo = new();
    private readonly string _appKey;

	public AptabaseClient(string appKey)
	{
        // TODO: change this
        _appKey = appKey;

		_http.BaseAddress = new Uri("http://localhost:5251");
		_http.DefaultRequestHeaders.Add("App-Key", _appKey);
    }

    public void TrackEvent(string eventName)
    {
		this.TrackEvent(eventName, null);
    }

    public void TrackEvent(string eventName, Dictionary<string, object> props)
	{
		SendEvent(eventName, props); // TODO: fix this
    }

	private async Task SendEvent(string eventName, Dictionary<string, object> props)
    {
        Console.WriteLine(_sysInfo.SdkVersion);
        try
        {
            var body = JsonContent.Create(new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                sessionId = Guid.NewGuid().ToString().ToLower(), // TODO: change this
                eventName,
                systemProps = new
                {
                    osName = _sysInfo.OsName,
                    osVersion = _sysInfo.OsVersion,
                    locale = _sysInfo.Locale,
                    appVersion = _sysInfo.AppVersion,
                    appBuildNumber = _sysInfo.AppBuildNumber,
                    sdkVersion = _sysInfo.SdkVersion,
                },
                props
            });

			var response = await _http.PostAsync("/v0/event", body);
			if (!response.IsSuccessStatusCode)
			{
				Console.WriteLine("Failed"); // TODO: change this
            }
        }
		catch
        {
            Console.WriteLine("Error"); // TODO: change this
        }
    }
}

