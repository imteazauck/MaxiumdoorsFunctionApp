
using MaxiumDoorsFunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MaxiumDoorsFunctionApp;
public sealed class AuthLoginFunction
{
    private readonly ILogger<AuthLoginFunction> _logger;
    private readonly IAuthRepository _authRepository;
    private readonly IJwtTokenService _jwtTokenService;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AuthLoginFunction(
        ILogger<AuthLoginFunction> logger,
        IAuthRepository authRepository,
        IJwtTokenService jwtTokenService)
    {
        _logger = logger;
        _authRepository = authRepository;
        _jwtTokenService = jwtTokenService;
    }

    [Function("AuthLogin")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "auth/login")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            return req.CreateResponse(HttpStatusCode.OK);
        }

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<LoginRequestDto>(req.Body, JsonOptions, cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteAsJsonAsync(new { error = "Email and password are required." }, cancellationToken);
                return bad;
            }

            var user = await _authRepository.GetUserByEmailAsync(payload.Email, cancellationToken);
            if (user is null || !user.IsActive || !PasswordHasher.VerifyPassword(payload.Password, user.PasswordHash))
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteAsJsonAsync(new { error = "Invalid credentials." }, cancellationToken);
                return unauthorized;
            }

            var token = _jwtTokenService.CreateToken(user);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new LoginResponseDto
            {
                Token = token,
                User = new AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Role = user.Role,
                    ResellerId = user.ResellerId,
                    DisplayName = user.DisplayName
                }
            }, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed unexpectedly.");
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { error = "Login failed." }, cancellationToken);
            return error;
        }
    }
}