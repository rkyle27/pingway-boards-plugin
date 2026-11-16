using System;

namespace PingwayBoards;

/// <summary>Which web addresses the plugin may open (always after a warning, with Dalamud's own open-link utility).</summary>
public static class UrlSafety
{
    public const string PingwayPrefix = "https://pingway.app/";

    /// <summary>A poster page on pingway.app: must start with https://pingway.app/ (nothing else is ever opened as "View full poster").</summary>
    public static bool IsPingway(string? url)
    {
        if (string.IsNullOrEmpty(url) || url.Length > 300 || !url.StartsWith(PingwayPrefix, StringComparison.Ordinal))
            return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme == Uri.UriSchemeHttps && uri.Host == "pingway.app" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo);
    }

    /// <summary>A link from a poster: https only, no user:password@, a real host name. Returns the cleaned address or null.</summary>
    public static string? SafeExternal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 300)
            return null;
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !uri.Host.Contains('.', StringComparison.Ordinal))
            return null;
        return uri.AbsoluteUri;
    }

    /// <summary>The site name shown in the warning, e.g. "example.com".</summary>
    public static string Domain(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return string.Empty;
        var host = uri.IdnHost;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}
