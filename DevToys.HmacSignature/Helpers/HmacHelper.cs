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
    /// <param name="url"></param>
    /// <param name="secretKey"></param>
    /// <param name="content"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static ResultInfo<HmacResponse> Generate(string httpMethod, string url, string secretKey, 
        string? content, HashAlgorithmEnum contentHashAlgorithm, HashAlgorithmEnum hmacHashAlgorithm,  ILogger logger)
    {
        try
        {
            if(!IsValidUrl(url))
            {
                return new(default, "Invalid URL", false);
            }
            // Specify the 'x-ms-date' header as the current UTC timestamp according to the RFC1123 standard.
            var date = DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture);
            // Compute a content hash for the 'x-ms-content-shaXXX' header.
            var contentHash = ComputeContentHash(content, contentHashAlgorithm);

            // Prepare a string to sign.
            var (host, pathAndQuery) = GetHostAndPathQuery(url);
            var stringToSign = $"{httpMethod.Trim().ToUpper()}\n{pathAndQuery}\n{date};{host};{contentHash}";
            // Compute the signature.
            var signature = ComputeSignature(stringToSign, hmacHashAlgorithm, secretKey);
            // Concatenate the string, which will be used in the authorization header.
            var authorizationHeader = $"HMAC-{GetHMACHashAlgorithm(hmacHashAlgorithm)} SignedHeaders=x-ms-date;host;x-ms-content-{GetContentHashAlgorithm(hmacHashAlgorithm)}&Signature={signature}";
            return new(new HmacResponse(date, contentHash, authorizationHeader), true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating HMAC signed header");
            return new(default, ex.Message, false);
        }
    }
    
    static string ComputeContentHash(string? content, HashAlgorithmEnum hashAlgorithm)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }
        using HashAlgorithm ha = GetHA(hashAlgorithm);
        byte[] hashedBytes = ha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hashedBytes);
    }
    
    static string ComputeSignature(string stringToSign, HashAlgorithmEnum hashAlgorithm, string? secretKey)
    {
        using HMAC hmac = GetHMAC(hashAlgorithm, secretKey);
        var bytes = Encoding.UTF8.GetBytes(stringToSign);
        var hashedBytes = hmac.ComputeHash(bytes);
        return Convert.ToBase64String(hashedBytes);
    }

    private static HashAlgorithm GetHA(HashAlgorithmEnum hashAlgorithm)
    {
        return hashAlgorithm switch
        {
            HashAlgorithmEnum.SHA1 => SHA1.Create(),
            HashAlgorithmEnum.SHA256 => SHA256.Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), hashAlgorithm, null)
        };
    }

    private static HMAC GetHMAC(HashAlgorithmEnum hashAlgorithm, string secretKey)
    {
        return hashAlgorithm switch
        {
            HashAlgorithmEnum.SHA1 => new HMACSHA1(Convert.FromBase64String(secretKey)),
            HashAlgorithmEnum.SHA256 => new HMACSHA256(Convert.FromBase64String(secretKey)),
            _ => throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), hashAlgorithm, null)
        };
    }

    private static bool IsValidUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out Uri? _);

    private static (string, string) GetHostAndPathQuery(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult))
        {
            var host = uriResult?.Authority;
            var pathAndQuery = uriResult?.PathAndQuery;
            return (host ?? string.Empty, pathAndQuery ?? string.Empty);
        }
        return (string.Empty, string.Empty);
    }

    static string GetHMACHashAlgorithm(HashAlgorithmEnum hmacHashAlgorithm)
    {
        return hmacHashAlgorithm switch
        {
            HashAlgorithmEnum.SHA1 => "SHA1",
            HashAlgorithmEnum.SHA256 => "SHA256",
            _ => throw new ArgumentOutOfRangeException(nameof(hmacHashAlgorithm), hmacHashAlgorithm, null)
        };
    }

    public static string GetContentHashAlgorithm(HashAlgorithmEnum hmacHashAlgorithm)
    {
        return hmacHashAlgorithm switch
        {
            HashAlgorithmEnum.SHA1 => "sha1",
            HashAlgorithmEnum.SHA256 => "sha256",
            _ => throw new ArgumentOutOfRangeException(nameof(hmacHashAlgorithm), hmacHashAlgorithm, null)
        };
    }

}

internal readonly struct HmacResponse
{
    public string? Date { get; }

    public string? ContentHash { get; }

    public string? AuthorizationHeader { get; }

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

internal enum HashAlgorithmEnum
{
    SHA1,
    SHA256,
}