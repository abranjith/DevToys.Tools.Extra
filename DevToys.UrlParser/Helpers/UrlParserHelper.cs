using DevToys.Api;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Web;

namespace DevToys.UrlParser.Helpers;

internal static class UrlParserHelper
{
    internal static async Task<ResultInfo<UrlParserResponse>> ParseAsync(
        string url,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                var schema = uri.Scheme;
                var port = uri.Port;
                var hostName = uri.Host;
                var urlPath = uri.AbsolutePath;
                var queryString = GetQueryStringValue(url);
                var ipinfo = await GetIPsForHostOrAddress(hostName, cancellationToken);

                return new(new UrlParserResponse(url, schema, port, hostName, urlPath, queryString, 
                    ipinfo.Where(r => r.IPv4 != null).Select(r => r.IPv4).ToList()!,
                    ipinfo.Where(r => r.IPv6 != null).Select(r => r.IPv6).ToList()!), true);
            }
            else
            {
                var ipinfo = await GetIPsForHostOrAddress(url, cancellationToken);
                var firstRecord = ipinfo.FirstOrDefault();
                return new(new UrlParserResponse(url, null, firstRecord.Port, firstRecord.HostName, null, null, 
                    ipinfo.Where(r => r.IPv4 != null).Select(r => r.IPv4).ToList()!,
                    ipinfo.Where(r => r.IPv6 != null).Select(r => r.IPv6).ToList()!), true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing URL");
            return new(new UrlParserResponse(url, ex.Message?? "Error parsing the address"), false);
        }
    }

    private static IList<KeyValuePair<string, string>> GetQueryStringValue(string url)
    {
        var queryString = new List<KeyValuePair<string, string>>();
        var urlParts = url.Split('?');
        if (urlParts.Length < 2)
            return queryString;

        var collection = HttpUtility.ParseQueryString(urlParts[1]);
        foreach (var key in collection.AllKeys)
        {
            queryString.Add(new KeyValuePair<string, string>(key ?? string.Empty, collection[key] ?? string.Empty));
        }
        return queryString;
    }

    private static async Task<IList<IpInfo>> GetIPsForHostOrAddress(string hostOrAddress, CancellationToken cancellationToken)
    {
        IList<IpInfo> info = [];

        if (IPEndPoint.TryParse(hostOrAddress, out var ip))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                info.Add(new IpInfo(ip.Port, await GetHostName(ip.Address.ToString(), cancellationToken), ip.Address.ToString(), null));
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(ip.Address))
            {
                info.Add(new IpInfo(ip.Port, await GetHostName(ip.Address.ToString(), cancellationToken), null, ip.Address.ToString()));
            }
        }
        else
        {
            var hostEntry = await Dns.GetHostEntryAsync(hostOrAddress, cancellationToken);
            foreach (var address in hostEntry.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    info.Add(new IpInfo(null, hostEntry.HostName, address.ToString(), null));
                }
                else if (address.AddressFamily == AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(address))
                {
                    info.Add(new IpInfo(null, hostEntry.HostName, null, address.ToString()));
                }
            }
        }
        return info;
    }

    private static async Task<string?> GetHostName(string address, CancellationToken cancellationToken)
    {
        try
        {
            var host = await Dns.GetHostEntryAsync(address, cancellationToken);
            return host.HostName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

internal readonly struct IpInfo
{
    public int? Port { get; }
    public string? HostName { get; }
    public string? IPv4 { get; }
    public string? IPv6 { get; }

    public IpInfo(int? port, string? hostName, string? ipv4, string? ipv6)
    {
        Port = port;
        HostName = hostName;
        IPv4 = ipv4;
        IPv6 = ipv6;
    }
}

internal readonly struct UrlParserResponse
{
    public string UrlString { get; }
    public string? Schema { get; }
    public int? Port { get; }
    public string? HostName { get; }
    public string? UrlPath { get; }
    public IList<KeyValuePair<string, string>> QueryString { get; } = [];
    public IList<string> IPv4 { get; } = [];
    public IList<string> IPv6 { get; } = [];
    public string? ErrorMessage { get; init; }

    public UrlParserResponse(string urlString, string? schema, int? port,
    string? hostName, string? urlPath, IList<KeyValuePair<string, string>>? queryString,
    IList<string>? ipv4s, IList<string>? ipv6s)
    {
        UrlString = urlString;
        Schema = schema;
        Port = port;
        HostName = hostName;
        UrlPath = urlPath;
        QueryString = queryString ?? [];
        IPv4 = ipv4s ?? [];
        IPv6 = ipv6s ?? [];
    }

    public UrlParserResponse(string urlString, string? errorMessage)
    {
        UrlString = urlString;
        ErrorMessage = errorMessage;
    }
}
