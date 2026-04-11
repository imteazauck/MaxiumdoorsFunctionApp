using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MaxiumDoorsFunctionApp;

public sealed class AuthContext
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? ResellerId { get; init; }
}

public static class AuthContextFactory
{
    public static AuthContext FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        return new AuthContext
        {
            UserId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty,
            Email = principal.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty,
            Role = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            ResellerId = principal.FindFirstValue("resellerId")
        };
    }
}