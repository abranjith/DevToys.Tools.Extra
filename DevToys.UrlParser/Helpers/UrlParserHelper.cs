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
        bool encodeUrl,
        ILogger logger,
        CancellationToken cancellationToken)
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
                var (IPv4, IPv6) = GetIPsForHostOrAddress(hostName);

                return new(new UrlParserResponse(url, schema, port, hostName, urlPath, queryString, IPv4, IPv6), true);
            }
            else
            {
                var (IPv4, IPv6) = GetIPsForHostOrAddress(url);
                var port = GetPort(url);
                var hostName = GetHostName(url);
                return new(new UrlParserResponse(url, null, port, hostName, null, null, IPv4, IPv6), true);
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

    private static (IList<string> IPv4, IList<string> IPv6) GetIPsForHostOrAddress(string hostOrAddress)
    {
        IList<string> ipv4s = [];
        IList<string> ipv6s = [];

        if (IPEndPoint.TryParse(hostOrAddress, out var ip))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                ipv4s.Add(ip.Address.ToString());
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(ip.Address))
            {
                ipv6s.Add(ip.Address.ToString());
            }
        }
        else
        {
            var hostEntry = Dns.GetHostEntry(hostOrAddress);
            foreach (var address in hostEntry.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipv4s.Add(address.ToString());
                }
                else if (address.AddressFamily == AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(address))
                {
                    ipv6s.Add(address.ToString());
                }
            }
        }
        return (ipv4s, ipv6s);
    }

    private static string? GetHostName(string address)
    {
        try
        {
            var host = Dns.GetHostEntry(address);
            return host.HostName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int? GetPort(string address)
    {
        if (IPEndPoint.TryParse(address, out var ip))
        {
            return ip.Port;
        }
        return null;
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