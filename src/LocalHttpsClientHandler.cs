#if ANDROID
using Xamarin.Android.Net;
#endif
#if IOS || MACCATALYST
using Foundation;
#endif

namespace Aptabase.Maui;

public class LocalHttpsClientHandler : DelegatingHandler
{
    public LocalHttpsClientHandler()
    {
#if ANDROID
        InnerHandler = new AndroidMessageHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (cert?.Issuer != null && cert.Issuer.Contains("CN=mkcert"))
                {
                    return true;
                }
                return errors == System.Net.Security.SslPolicyErrors.None;
            }
        };
#elif IOS || MACCATALYST
        InnerHandler = new NSUrlSessionHandler
        {
            TrustOverrideForUrl = (sender, url, trust) => url.StartsWith("https://localhost"),
        };
#else
        InnerHandler = new HttpClientHandler();
#endif
    }
}