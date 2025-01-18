using DevToys.Api;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace DevToys.Pkce.Helpers;

internal class PkceHelper
{
    /// <summary>
    /// Generate a random string for the code verifier and code challenge
    /// https://datatracker.ietf.org/doc/html/rfc7636#section-4.1
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public static ResultInfo<PkceResponse> Generate(int size, string? verifier, ILogger logger)
    {
        try
        {
            if (size < 43 || size > 128)
                size = 128;

            if(string.IsNullOrWhiteSpace(verifier))
                verifier = GetCodeVerifier(size);
            var challenge = GetCodeChallenge(verifier);

            return new (new PkceResponse(verifier, challenge), true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating PKCE");
            return new(default, ex.Message, false);
        }
    }
    
    /// <summary>
    /// ABNF for "code_verifier" is as follows.
    ///     code-verifier = 43*128unreserved
    ///     unreserved = ALPHA / DIGIT / "-" / "." / "_" / "~"
    ///     ALPHA = %x41-5A / %x61-7A
    ///     DIGIT = % x30 - 39
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    private static string GetCodeVerifier(int size)
    {
        const string validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        
        char[] bytes = new char[size];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = validChars[Random.Shared.Next(validChars.Length)];
        }

        return new string(bytes);
    }

    /// <summary>
    /// ABNF for "code_challenge" is as follows.
    /// plain
    ///     code_challenge = code_verifier
    /// S256
    ///     code_challenge = BASE64URL-ENCODE(SHA256(ASCII(code_verifier)))
    /// </summary>
    /// <param name="verifier"></param>
    /// <returns></returns>
    private static string GetCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        var challenge = Base64UrlEncode(hash);
        return challenge;
    }


    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
}

internal readonly struct PkceResponse
{
    public string? CodeVerifier { get; }

    public string? CodeChallenge { get; }

    public string Method { get; } = "S256";

    public PkceResponse(string codeVerifier, string codeChallenge)
    {
        CodeVerifier = codeVerifier;
        CodeChallenge = codeChallenge;
    }

    public PkceResponse()
    {
    }
}
