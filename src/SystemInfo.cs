using System.Diagnostics;
using System.Reflection;

namespace Aptabase.Maui;

internal class SystemInfo
{
    private static readonly string _pkgVersion = typeof(AptabaseClient).Assembly
        .GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version;

    public bool IsDebug { get; set; }
    public string OsName { get; }
    public string OsVersion { get; }
    public string SdkVersion { get; }
    public string Locale { get; }
    public string AppVersion { get; }
    public string AppBuildNumber { get; }

    public SystemInfo()
	{
        OsName = GetOsName();
        OsVersion = GetOsVersion();
        SdkVersion = $"Aptabase.Maui@{_pkgVersion}";
        Locale = Thread.CurrentThread.CurrentCulture.Name;
        AppVersion = AppInfo.Current.VersionString;
        AppBuildNumber = AppInfo.Current.BuildString;
    }
        
    internal static bool IsInDebugMode(Assembly? assembly)
    {
        if (assembly == null)
            return false;

        var attributes = assembly.GetCustomAttributes(typeof(DebuggableAttribute), false);
        if (attributes.Length > 0)
        {
            if (attributes[0] is DebuggableAttribute debuggable)
                return (debuggable.DebuggingFlags & DebuggableAttribute.DebuggingModes.Default) == DebuggableAttribute.DebuggingModes.Default;
            else
                return false;
        }
        else
            return false;
    }

    private static string GetOsName()
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

    private static string GetOsVersion()
    {
#if MACCATALYST
        var osVersion = Foundation.NSProcessInfo.ProcessInfo.OperatingSystemVersion;
        return $"{osVersion.Major}.{osVersion.Minor}.{osVersion.PatchVersion}";
#else
        return DeviceInfo.Current.VersionString;
#endif
    }
}

