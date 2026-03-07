namespace Aptabase.Core;

internal static class DistroDetect
{
    // Linux distributions also have /var/lib/lsb-release but I wanted to keep this simple.
    public static (string Name, string Version) GetLinuxInfo()
    {
        const string osReleaseFile = "/etc/os-release";

        // Try reading /etc/os-release
        try
        {
            var data = File.ReadAllText(osReleaseFile);
            return ParseOsRelease(data);
        }
        catch (IOException)
        {
            // Fallback to lsb_release if /etc/os-release is inaccessible (e.g., under firejail)
            return ParseLsbReleaseOrFallback();
        }
    }
        private static (string Name, string Version) ParseOsRelease(string data)
    {
        string name = string.Empty, version = string.Empty;

        var lines = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("NAME="))
            {
                name = TrimValue(line);
            }
            else if (line.StartsWith("VERSION_ID="))
            {
                version = TrimValue(line);
            }
            else if (line.StartsWith("VERSION=") && string.IsNullOrEmpty(version))
            {
                version = TrimValue(line);
            }
            else if (line.StartsWith("VERSION_CODENAME=") && string.IsNullOrEmpty(version))
            {
                version = TrimValue(line);
            }
        }

        return (name, version);
    }

    private static (string Name, string Version) ParseLsbReleaseOrFallback()
    {
        // Fallback parsing if /etc/os-release is unavailable, using lsb_release command
        // I know the file exists, but according to a StackOverflow wizard, it shouldn't be used.
        try
        {
            var lsbReleaseOutput = ExecuteCommand("lsb_release", "-a");
            string name = string.Empty, version = string.Empty;

            foreach (var line in lsbReleaseOutput.Split('\n'))
            {
                if (line.StartsWith("Distributor ID:"))
                {
                    name = line.Split(new[] { ':' }, 2)[1].Trim();
                }
                else if (line.StartsWith("Release:"))
                {
                    version = line.Split(new[] { ':' }, 2)[1].Trim();
                }
            }

            return (name, version);
        }
        catch (Exception)
        {
            return ("", "");
        }
    }

    private static string TrimValue(string line)
    {
        // Extracts and trims the value from the line, removing the surrounding quotes
        return line.Split(new[] { '=' }, 2)[1].Trim('\"');
    }

    private static string ExecuteCommand(string command, string arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null) return "";

        return process.StandardOutput.ReadToEnd();
    }
    internal static string GetLinuxDistro()
    {
        var distroName = GetLinuxInfo().Name;
        return string.IsNullOrWhiteSpace(distroName) ? "Linux" : distroName;
    }
    internal static string GetLinuxDistroVersion()
    {
        var distroVersion = GetLinuxInfo().Version;
        return string.IsNullOrWhiteSpace(distroVersion) ? Environment.OSVersion.Version.ToString() : distroVersion;
    }
}