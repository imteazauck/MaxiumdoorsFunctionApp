using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace MaxiumDoorsFunctionApp;

public static class FunctionHttp
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string GetAllowedOrigin(IConfiguration configuration) =>
        configuration["AllowedOrigin"]
        ?? configuration["Values:AllowedOrigin"]
        ?? "http://localhost:5173";

    public static void AddCorsHeaders(HttpResponseData response, string allowedOrigin)
    {
        response.Headers.Add("Access-Control-Allow-Origin", allowedOrigin);
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, x-functions-key");
    }

    public static HttpResponseData CreateCorsResponse(HttpRequestData req, HttpStatusCode statusCode, string allowedOrigin)
    {
        var response = req.CreateResponse(statusCode);
        AddCorsHeaders(response, allowedOrigin);
        return response;
    }

    public static async Task<HttpResponseData> CreateJsonResponseAsync<T>(
        HttpRequestData req,
        HttpStatusCode statusCode,
        T payload,
        string allowedOrigin,
        CancellationToken cancellationToken)
    {
        var response = CreateCorsResponse(req, statusCode, allowedOrigin);
        await response.WriteAsJsonAsync(payload, cancellationToken);
        return response;
    }

    public static async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message,
        string allowedOrigin,
        CancellationToken cancellationToken)
    {
        return await CreateJsonResponseAsync(req, statusCode, new { error = message }, allowedOrigin, cancellationToken);
    }

    public static bool IsOptions(string? method) =>
        string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase);

    public static string? GetBearerToken(HttpRequestData req)
    {
        if (!req.Headers.TryGetValues("Authorization", out var values))
        {
            return null;
        }

        var header = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header[7..].Trim();
    }

    public static async Task<ClaimsPrincipal?> ValidateAuthenticatedAsync(
        HttpRequestData req,
        IJwtValidator jwtValidator,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var token = GetBearerToken(req);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return jwtValidator.Validate(token);
    }

    public static async Task<ClaimsPrincipal?> ValidateAdminAsync(
        HttpRequestData req,
        IJwtValidator jwtValidator,
        CancellationToken cancellationToken)
    {
        var principal = await ValidateAuthenticatedAsync(req, jwtValidator, cancellationToken);
        if (principal is null || !IsInRole(principal, UserRoles.Admin))
        {
            return null;
        }

        return principal;
    }

    public static bool IsInRole(ClaimsPrincipal principal, string role)
    {
        var value = principal.FindFirst(ClaimTypes.Role)?.Value ?? principal.FindFirst("role")?.Value;
        return string.Equals(value, role, StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetResellerId(ClaimsPrincipal principal) =>
        principal.FindFirst("resellerId")?.Value;
}
