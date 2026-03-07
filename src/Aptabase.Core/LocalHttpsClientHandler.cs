using System.Net;
using System.Net.Sockets;

namespace Aptabase.Core;

public class LocalHttpsClientHandler : HttpClientHandler
{
    public LocalHttpsClientHandler()
    {
        // Disable SSL verification
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
        {
            var requestUri = sender.RequestUri;
            if (requestUri is null || requestUri.Scheme != "https") return false;
            return IsLocalAddress(requestUri.Host);
        };
    }
        /// <summary>
        /// Checks if a given host string represents a local IP address.
        /// This includes loopback and common private IP ranges.
        /// </summary>
        /// <param name="host">The host string (IP address or hostname).</param>
        /// <returns>True if the host is considered local, false otherwise.</returns>
       private static bool IsLocalAddress(string host)
    {
        try
        {
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            bool CheckIp(IPAddress ipAddr)
            {
                if (IPAddress.IsLoopback(ipAddr))
                {
                    return true;
                }

                switch (ipAddr.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                    {
                        var ipBytes = ipAddr.GetAddressBytes();
                        switch (ipBytes[0])
                        {
                            case 10:
                            case 172 when (ipBytes[1] >= 16 && ipBytes[1] <= 31):
                                return true;
                        }

                        if (ipBytes[0] == 192 && ipBytes[1] == 168) return true;
                        break;
                    }
                    case AddressFamily.InterNetworkV6:
                    {
                        var ipBytes = ipAddr.GetAddressBytes();
                        switch (ipBytes[0])
                        {
                            case 0xFC:
                            case 0xFD:
                            case 0xFE when (ipBytes[1] & 0xC0) == 0x80:
                                return true;
                        }

                        break;
                    }
                }

                return false;
            }

            if (IPAddress.TryParse(host, out var ipAddress))
            {
                if (CheckIp(ipAddress))
                {
                    return true;
                }
            }
            else
            {
                var hostEntry = Dns.GetHostEntry(host);
                if (hostEntry.AddressList.Any(CheckIp))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore
        }
        return false;
    }
}