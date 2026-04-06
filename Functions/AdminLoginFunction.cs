using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class AdminLoginFunction
{
    private readonly AdminAuthService _adminAuthService;
    private readonly ILogger<AdminLoginFunction> _logger;
    private readonly string _allowedOrigin;

    public AdminLoginFunction(AdminAuthService adminAuthService, ILogger<AdminLoginFunction> logger, IConfiguration configuration)
    {
        _adminAuthService = adminAuthService;
        _logger = logger;
        _allowedOrigin = configuration["AllowedOrigin"]
            ?? configuration["Values:AllowedOrigin"]
            ?? "http://localhost:5173";
    }

    [Function("AdminLogin")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "backoffice/login")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<AdminLoginRequestDto>(req.Body, FunctionHttp.JsonOptions, cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Username) || string.IsNullOrWhiteSpace(payload.Password))
            {
                return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Username and password are required.", _allowedOrigin, cancellationToken);
            }

            if (!_adminAuthService.IsValidCredential(payload.Username, payload.Password))
            {
                return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Invalid admin credentials.", _allowedOrigin, cancellationToken);
            }

            var result = _adminAuthService.CreateLoginResponse(payload.Username);
            return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, result, _allowedOrigin, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid admin login JSON payload received.");
            return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Invalid request payload.", _allowedOrigin, cancellationToken);
        }
    }
}
