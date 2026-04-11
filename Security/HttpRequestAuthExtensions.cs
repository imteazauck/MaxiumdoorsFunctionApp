using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace MaxiumDoorsFunctionApp;

public static class HttpRequestAuthExtensions
{
    /// <summary>
    /// Extracts the Bearer token from the Authorization header.
    /// </summary>
    public static string? GetBearerToken(this HttpRequestData req)
    {
        if (!req.Headers.TryGetValues("Authorization", out var values))
        {
            return null;
        }

        var header = values.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header.Substring("Bearer ".Length).Trim();
    }

    /// <summary>
    /// Creates a 401 Unauthorized response with JSON body.
    /// </summary>
    public static async Task<HttpResponseData> CreateUnauthorizedAsync(
        this HttpRequestData req,
        string message,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.Unauthorized);

        await response.WriteAsJsonAsync(new
        {
            error = message
        }, cancellationToken);

        return response;
    }

    /// <summary>
    /// Creates a 403 Forbidden response with JSON body.
    /// </summary>
    public static async Task<HttpResponseData> CreateForbiddenAsync(
        this HttpRequestData req,
        string message,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.Forbidden);

        await response.WriteAsJsonAsync(new
        {
            error = message
        }, cancellationToken);

        return response;
    }
}