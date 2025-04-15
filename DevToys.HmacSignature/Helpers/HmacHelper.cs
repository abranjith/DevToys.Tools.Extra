using DevToys.Api;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DevToys.HmacSignature.Helpers;

internal class HmacHelper
{
    /// <summary>
    /// Generate HMAC Signature for the request.
    /// Reference: https://learn.microsoft.com/en-us/azure/communication-services/tutorials/hmac-header-tutorial?pivots=programming-language-csharp
    /// </summary>
    /// <param name="httpMethod"></param>
    /// <param name="pathAndQuery"></param>
    /// <param name="content"></param>
    /// <param name="host"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static ResultInfo<HmacResponse> Generate(string httpMethod, string pathAndQuery, string? content, string? host, ILogger logger)
    {
        try
        {
            // Specify the 'x-ms-date' header as the current UTC timestamp according to the RFC1123 standard.
            var date = DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture);
            // Compute a content hash for the 'x-ms-content-sha256' header.
            var contentHash = ComputeContentHash(content);

            // Prepare a string to sign.
            var stringToSign = $"{httpMethod.Trim().ToUpper()}\n{pathAndQuery}\n{date};{host};{contentHash}";
            // Compute the signature.
            var signature = ComputeSignature(stringToSign);
            // Concatenate the string, which will be used in the authorization header.
            var authorizationHeader = $"HMAC-SHA256 SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature={signature}";
            return new(new HmacResponse(date, contentHash, authorizationHeader), true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating PKCE");
            return new(default, ex.Message, false);
        }
    }

    static string ComputeContentHash(string? content)
    {
        if(string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }
        byte[] hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hashedBytes);
    }

    static string ComputeSignature(string stringToSign)
    {
        string secret = "resourceAccessKey";
        using var hmacsha256 = new HMACSHA256(Convert.FromBase64String(secret));
        var bytes = Encoding.UTF8.GetBytes(stringToSign);
        var hashedBytes = hmacsha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashedBytes);
    }
}

internal readonly struct HmacResponse
{
    public string? Date { get; }

    public string? ContentHash { get; }

    public string? AuthorizationHeader { get; }

    public string Method { get; } = "S256";

    public HmacResponse(string date, string contenthash, string authorizationHeader)
    {
        Date = date;
        ContentHash = contenthash;
        AuthorizationHeader = authorizationHeader;
    }

    public HmacResponse()
    {
    }
}