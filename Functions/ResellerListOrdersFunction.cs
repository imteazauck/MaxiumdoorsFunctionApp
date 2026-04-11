using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Security.Claims;

namespace MaxiumDoorsFunctionApp;

public sealed class ResellerListOrdersFunction
{
    private readonly IJwtValidator _jwtValidator;
    private readonly IConfiguration _configuration;

    public ResellerListOrdersFunction(IJwtValidator jwtValidator, IConfiguration configuration)
    {
        _jwtValidator = jwtValidator;
        _configuration = configuration;
    }

    [Function("ResellerListOrders")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "reseller/orders")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var allowedOrigin = FunctionHttp.GetAllowedOrigin(_configuration);

        if (string.Equals(req.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, allowedOrigin);
        }

        var token = req.GetBearerToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return await req.CreateUnauthorizedAsync("Missing bearer token.", cancellationToken);
        }

        var principal = _jwtValidator.Validate(token);
        if (principal is null)
        {
            return await req.CreateUnauthorizedAsync("Invalid token.", cancellationToken);
        }

        var role = principal.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, UserRoles.Reseller, StringComparison.OrdinalIgnoreCase))
        {
            return await req.CreateForbiddenAsync("Reseller access required.", cancellationToken);
        }

        var resellerId = principal.FindFirstValue("resellerId");
        if (string.IsNullOrWhiteSpace(resellerId))
        {
            return await req.CreateForbiddenAsync("Reseller ID is missing.", cancellationToken);
        }

        var ok = req.CreateResponse(HttpStatusCode.OK);
        FunctionHttp.AddCorsHeaders(ok, allowedOrigin);

        await ok.WriteAsJsonAsync(new
        {
            message = "Reseller orders endpoint reached.",
            resellerId
        }, cancellationToken);

        return ok;
    }
}