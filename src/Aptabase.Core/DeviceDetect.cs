using System.Diagnostics;
using System.Runtime.InteropServices;
#pragma warning disable CA1416 // I like to live dangerously.
using Microsoft.Win32;

namespace Aptabase.Core;

internal static class DeviceDetect
{
    internal static string GetDeviceModel()
    {
        return RuntimeInformation.OSDescription.ToLower() switch
        {
            var os when OperatingSystem.IsWindows() => GetWindowsDeviceModel(),
            var os when OperatingSystem.IsMacOS() => GetMacDeviceModel(),
            var os when OperatingSystem.IsLinux() => GetLinuxDeviceModel(),
            var os when OperatingSystem.IsFreeBSD() => GetFreeBSDDeviceModel(),
            _ => string.Empty // Unsupported Device Detection Platform
        };
    }

    private static string GetWindowsDeviceModel()
    {
        const string registryKeyPath = @"HARDWARE\DESCRIPTION\System\BIOS";
        const string registryValueName = "SystemProductName";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(registryKeyPath);
            if (key == null) return string.Empty;

            var model = key.GetValue(registryValueName) as string;

            return !string.IsNullOrEmpty(model) ? model : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    private static string GetMacDeviceModel()
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "sysctl",
            Arguments = "-n hw.model",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null) return string.Empty;

        var output = process.StandardOutput.ReadToEnd();
        var modelIdentifier = output.Trim();

        return string.IsNullOrEmpty(modelIdentifier) ? string.Empty : modelIdentifier;
    }

    private static string GetLinuxDeviceModel()
    {
        try
        {
            var model = File.ReadAllText("/sys/class/dmi/id/product_name");
            return model.Trim();
        }
        catch (Exception)
        {
            return string.Empty; 
        }
    }

    private static string GetFreeBSDDeviceModel()
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "kenv",
            Arguments = "smbios.system.product",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null) return string.Empty;

        var output = process.StandardOutput.ReadToEnd();
        var deviceModel = output.Trim();

        return string.IsNullOrEmpty(deviceModel) ? string.Empty : deviceModel;
    }
}