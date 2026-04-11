using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MaxiumDoorsFunctionApp;

public interface IJwtValidator
{
    ClaimsPrincipal? Validate(string token);
}

public sealed class JwtValidator : IJwtValidator
{
    private readonly IConfiguration _configuration;

    public JwtValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ClaimsPrincipal? Validate(string token)
    {
        var secret = _configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("Jwt:Key is missing.");

        var issuer = _configuration["Jwt:Issuer"] ?? "maxiumdoors-api";
        var audience = _configuration["Jwt:Audience"] ?? "maxiumdoors-client";

        var tokenHandler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        try
        {
            return tokenHandler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }
}