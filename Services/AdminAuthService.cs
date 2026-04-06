using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class AdminAuthService
{
    private readonly string _username;
    private readonly string _password;
    private readonly string _secret;
    private readonly int _tokenLifetimeHours;

    public AdminAuthService(IConfiguration configuration)
    {
        _username = configuration["AdminAuth:Username"]
            ?? configuration["Values:AdminAuth:Username"]
            ?? "admin";

        _password = configuration["AdminAuth:Password"]
            ?? configuration["Values:AdminAuth:Password"]
            ?? "ChangeMe123!";

        _secret = configuration["AdminAuth:Secret"]
            ?? configuration["Values:AdminAuth:Secret"]
            ?? "replace-this-secret-for-production";

        _tokenLifetimeHours = int.TryParse(
            configuration["AdminAuth:TokenLifetimeHours"] ?? configuration["Values:AdminAuth:TokenLifetimeHours"],
            out var tokenLifetimeHours)
            ? Math.Max(1, tokenLifetimeHours)
            : 12;
    }

    public bool IsValidCredential(string username, string password) =>
        string.Equals(username?.Trim(), _username, StringComparison.OrdinalIgnoreCase)
        && string.Equals(password, _password, StringComparison.Ordinal);

    public AdminLoginResponseDto CreateLoginResponse(string username)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(_tokenLifetimeHours);
        return new AdminLoginResponseDto
        {
            username = username.Trim(),
            expiresAt = expiresAt.ToString("O"),
            token = CreateToken(username.Trim(), expiresAt)
        };
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return null;
        }

        var expectedSignature = ComputeSignature(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(parts[1]),
                Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return null;
        }

        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var payload = JsonSerializer.Deserialize<TokenPayload>(payloadJson);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Sub) || payload.Exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                return null;
            }

            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, payload.Sub),
                new Claim(ClaimTypes.Role, "admin")
            ],
            authenticationType: "CustomBearer");

            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return null;
        }
    }

    private string CreateToken(string username, DateTimeOffset expiresAt)
    {
        var payload = new TokenPayload
        {
            Sub = username,
            Exp = expiresAt.ToUnixTimeSeconds()
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signature = ComputeSignature(encodedPayload);
        return $"{encodedPayload}.{signature}";
    }

    private string ComputeSignature(string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var normalized = input.Replace('-', '+').Replace('_', '/');
        var padding = 4 - (normalized.Length % 4);
        if (padding is > 0 and < 4)
        {
            normalized += new string('=', padding);
        }

        return Convert.FromBase64String(normalized);
    }

    private sealed class TokenPayload
    {
        public string Sub { get; set; } = string.Empty;
        public long Exp { get; set; }
    }
}
