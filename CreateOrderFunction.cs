using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MaxiumDoorsFunctionApp;

public class CreateOrderFunction
{
    private readonly CosmosOrderRepository _repository;
    private readonly ILogger<CreateOrderFunction> _logger;
    private readonly string _allowedOrigin;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public CreateOrderFunction(
        CosmosOrderRepository repository,
        ILogger<CreateOrderFunction> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _logger = logger;

        _allowedOrigin = configuration["AllowedOrigin"]
            ?? configuration["Values:AllowedOrigin"]
            ?? "http://localhost:5173";

    }

    [Function("CreateOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "orders")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (HttpMethods.IsOptions(req.Method))
        {
            var preflightResponse = req.CreateResponse(HttpStatusCode.OK);
            AddCorsHeaders(preflightResponse);
            return preflightResponse;
        }

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<CreateOrderRequestDto>(
                req.Body,
                JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Request body is required.", cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(payload.CustomerDetails?.Email))
            {
                return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Customer email is required.", cancellationToken);
            }

            if (payload.Doors is null || payload.Doors.Count == 0)
            {
                return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "At least one door is required.", cancellationToken);
            }

            var result = await _repository.CreateOrderAsync(payload, cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.Created);
            AddCorsHeaders(response);
            await response.WriteAsJsonAsync(result, cancellationToken);
            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid order JSON payload received.");
            return await CreateErrorResponseAsync(req, HttpStatusCode.BadRequest, "Invalid request payload.", cancellationToken);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB failure while creating order. Status code: {StatusCode}", ex.StatusCode);
            return await CreateErrorResponseAsync(
                req,
                HttpStatusCode.InternalServerError,
                "The order could not be saved. Check Cosmos DB configuration and container settings.",
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Application configuration error while creating order.");
            return await CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating order.");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            AddCorsHeaders(response);
            await response.WriteAsJsonAsync(new
            {
                error = ex.Message,
                inner = ex.InnerException?.Message
            }, cancellationToken);
            return response;
        }
    }

    private async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(statusCode);
        AddCorsHeaders(response);
        await response.WriteAsJsonAsync(new { error = message }, cancellationToken);
        return response;
    }

    private void AddCorsHeaders(HttpResponseData response)
    {
        response.Headers.Add("Access-Control-Allow-Origin", _allowedOrigin);
        response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, x-functions-key");
    }

    private static class HttpMethods
    {
        public static bool IsOptions(string? method) =>
            string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase);
    }
}