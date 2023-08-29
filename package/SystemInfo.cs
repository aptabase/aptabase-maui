using System.Diagnostics;
using System.Reflection;

namespace Aptabase.Maui;

internal class SystemInfo
{
    private static string _pkgVersion = typeof(AptabaseClient).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    public bool IsDebug { get; private set; }
    public string OsName { get; }
	public string OsVersion { get; }
	public string SdkVersion { get; }
    public string Locale { get; }
    public string AppVersion { get; }
    public string AppBuildNumber { get; }

    public SystemInfo()
	{
        this.MaybeSetDebugMode();

        this.OsName = GetOsName();
        this.OsVersion = GetOsVersion();
        this.SdkVersion = $"Aptabase.Maui@{_pkgVersion}";
        this.Locale = Thread.CurrentThread.CurrentCulture.Name;
        this.AppVersion = AppInfo.Current.VersionString;
        this.AppBuildNumber = AppInfo.Current.BuildString;
    }

    [Conditional("DEBUG")]
    public void MaybeSetDebugMode()
    {
        this.IsDebug = true;
    }

    private string GetOsName()
	{
		var platform = DeviceInfo.Current.Platform;
		if (platform == DevicePlatform.Android)
			return "Android";

        if (platform == DevicePlatform.WinUI)
            return "Windows";

        if (platform == DevicePlatform.macOS || platform == DevicePlatform.MacCatalyst)
            return "macOS";

        if (platform == DevicePlatform.tvOS)
            return "tvOS";

        if (platform == DevicePlatform.watchOS)
            return "watchOS";

        if (platform == DevicePlatform.Tizen)
            return "Tizen";

        if (platform == DevicePlatform.iOS)
        {
            if (DeviceInfo.Current.Idiom == DeviceIdiom.Tablet && DeviceInfo.Current.Version.Major >= 13)
                return "iPadOS";

            return "iOS";
        }

        return "";
    }

    private string GetOsVersion()
    {
#if MACCATALYST
        var osVersion = Foundation.NSProcessInfo.ProcessInfo.OperatingSystemVersion;
        return $"{osVersion.Major}.{osVersion.Minor}.{osVersion.PatchVersion}";
#else
        return DeviceInfo.Current.VersionString;
#endif
    }
}

