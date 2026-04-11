using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class AdminResellersFunction
{
    private readonly CosmosResellerRepository _repository;
    private readonly IJwtValidator _jwtValidator;
    private readonly ILogger<AdminResellersFunction> _logger;
    private readonly string _allowedOrigin;

    public AdminResellersFunction(
        CosmosResellerRepository repository,
        IJwtValidator jwtValidator,
        ILogger<AdminResellersFunction> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _jwtValidator = jwtValidator;
        _logger = logger;
        _allowedOrigin = FunctionHttp.GetAllowedOrigin(configuration);
    }

    [Function("AdminResellers")]
    public async Task<HttpResponseData> Resellers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options", Route = "backoffice/resellers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        var principal = await FunctionHttp.ValidateAdminAsync(req, _jwtValidator, cancellationToken);
        if (principal is null)
        {
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.Unauthorized,
                "Admin authentication is required.",
                _allowedOrigin,
                cancellationToken);
        }

        try
        {
            if (HttpMethods.IsGet(req.Method))
            {
                var items = await _repository.ListResellersAsync(cancellationToken);
                return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, items, _allowedOrigin, cancellationToken);
            }

            if (HttpMethods.IsPost(req.Method))
            {
                var payload = await JsonSerializer.DeserializeAsync<ResellerUpsertRequestDto>(
                    req.Body,
                    FunctionHttp.JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.CompanyName))
                {
                    return await FunctionHttp.CreateErrorResponseAsync(
                        req,
                        HttpStatusCode.BadRequest,
                        "Company Name is required.",
                        _allowedOrigin,
                        cancellationToken);
                }

                var created = await _repository.CreateResellerAsync(payload, cancellationToken);
                return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.Created, created, _allowedOrigin, cancellationToken);
            }

            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.MethodNotAllowed,
                "Method not allowed.",
                _allowedOrigin,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process reseller collection request. Method: {Method}", req.Method);
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to process reseller request.",
                _allowedOrigin,
                cancellationToken);
        }
    }

    [Function("AdminResellerById")]
    public async Task<HttpResponseData> ResellerById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "put", "delete", "options", Route = "backoffice/resellers/{resellerId}")] HttpRequestData req,
        string resellerId,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        var principal = await FunctionHttp.ValidateAdminAsync(req, _jwtValidator, cancellationToken);
        if (principal is null)
        {
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.Unauthorized,
                "Admin authentication is required.",
                _allowedOrigin,
                cancellationToken);
        }

        try
        {
            if (HttpMethods.IsGet(req.Method))
            {
                var reseller = await _repository.GetResellerAsync(resellerId, cancellationToken);
                if (reseller is null)
                {
                    return await FunctionHttp.CreateErrorResponseAsync(
                        req,
                        HttpStatusCode.NotFound,
                        "Reseller not found.",
                        _allowedOrigin,
                        cancellationToken);
                }

                return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, reseller, _allowedOrigin, cancellationToken);
            }

            if (HttpMethods.IsPut(req.Method))
            {
                var payload = await JsonSerializer.DeserializeAsync<ResellerUpsertRequestDto>(
                    req.Body,
                    FunctionHttp.JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.CompanyName))
                {
                    return await FunctionHttp.CreateErrorResponseAsync(
                        req,
                        HttpStatusCode.BadRequest,
                        "Company Name is required.",
                        _allowedOrigin,
                        cancellationToken);
                }

                var updated = await _repository.UpdateResellerAsync(resellerId, payload, cancellationToken);
                if (updated is null)
                {
                    return await FunctionHttp.CreateErrorResponseAsync(
                        req,
                        HttpStatusCode.NotFound,
                        "Reseller not found.",
                        _allowedOrigin,
                        cancellationToken);
                }

                return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, updated, _allowedOrigin, cancellationToken);
            }

            if (HttpMethods.IsDelete(req.Method))
            {
                var deleted = await _repository.DeleteResellerAsync(resellerId, cancellationToken);
                if (!deleted)
                {
                    return await FunctionHttp.CreateErrorResponseAsync(
                        req,
                        HttpStatusCode.NotFound,
                        "Reseller not found.",
                        _allowedOrigin,
                        cancellationToken);
                }

                return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.NoContent, _allowedOrigin);
            }

            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.MethodNotAllowed,
                "Method not allowed.",
                _allowedOrigin,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process reseller request for {ResellerId}. Method: {Method}", resellerId, req.Method);
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to process reseller request.",
                _allowedOrigin,
                cancellationToken);
        }
    }

    [Function("AdminResellerPricing")]
    public async Task<HttpResponseData> ResellerPricing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "backoffice/resellers/{resellerId}/pricing")] HttpRequestData req,
        string resellerId,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        var principal = await FunctionHttp.ValidateAdminAsync(req, _jwtValidator, cancellationToken);
        if (principal is null)
        {
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.Unauthorized,
                "Admin authentication is required.",
                _allowedOrigin,
                cancellationToken);
        }

        try
        {
            var items = await _repository.GetPricingAsync(resellerId, cancellationToken);
            return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, items, _allowedOrigin, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pricing for reseller {ResellerId}.", resellerId);
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to load reseller pricing.",
                _allowedOrigin,
                cancellationToken);
        }
    }

    [Function("AdminResellerPricingItem")]
    public async Task<HttpResponseData> ResellerPricingItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "backoffice/resellers/{resellerId}/pricing/{itemId}")] HttpRequestData req,
        string resellerId,
        string itemId,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        var principal = await FunctionHttp.ValidateAdminAsync(req, _jwtValidator, cancellationToken);
        if (principal is null)
        {
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.Unauthorized,
                "Admin authentication is required.",
                _allowedOrigin,
                cancellationToken);
        }

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<ResellerPricingUpdateRequestDto>(
                req.Body,
                FunctionHttp.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return await FunctionHttp.CreateErrorResponseAsync(
                    req,
                    HttpStatusCode.BadRequest,
                    "Pricing payload is required.",
                    _allowedOrigin,
                    cancellationToken);
            }

            var updated = await _repository.UpdatePricingItemAsync(resellerId, itemId, payload.Price, cancellationToken);
            if (updated is null)
            {
                return await FunctionHttp.CreateErrorResponseAsync(
                    req,
                    HttpStatusCode.NotFound,
                    "Pricing item not found.",
                    _allowedOrigin,
                    cancellationToken);
            }

            return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, updated, _allowedOrigin, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update reseller pricing item {ItemId} for reseller {ResellerId}.", itemId, resellerId);
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to update pricing item.",
                _allowedOrigin,
                cancellationToken);
        }
    }

    [Function("AdminResellerCredentials")]
    public async Task<HttpResponseData> ResellerCredentials(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options", Route = "backoffice/resellers/{resellerId}/credentials")] HttpRequestData req,
        string resellerId,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        var principal = await FunctionHttp.ValidateAdminAsync(req, _jwtValidator, cancellationToken);
        if (principal is null)
        {
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.Unauthorized,
                "Admin authentication is required.",
                _allowedOrigin,
                cancellationToken);
        }

        try
        {
            if (HttpMethods.IsGet(req.Method))
            {
                var credentials = await _repository.GetCredentialStatusAsync(resellerId, cancellationToken);
                if (credentials is null)
                {
                    return await FunctionHttp.CreateErrorResponseAsync(
                        req,
                        HttpStatusCode.NotFound,
                        "Reseller not found.",
                        _allowedOrigin,
                        cancellationToken);
                }

                credentials.PasswordPlaintext = null;
                return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, credentials, _allowedOrigin, cancellationToken);
            }

            if (HttpMethods.IsPost(req.Method))
            {
                var payload = await JsonSerializer.DeserializeAsync<ResellerCredentialUpdateRequestDto>(
                                  req.Body,
                                  FunctionHttp.JsonOptions,
                                  cancellationToken)
                              ?? new ResellerCredentialUpdateRequestDto();

                var updated = await _repository.UpsertCredentialsAsync(resellerId, payload, cancellationToken);
                if (updated is null)
                {
                    return await FunctionHttp.CreateErrorResponseAsync(
                        req,
                        HttpStatusCode.NotFound,
                        "Reseller not found.",
                        _allowedOrigin,
                        cancellationToken);
                }

                updated.PasswordPlaintext = null;
                return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, updated, _allowedOrigin, cancellationToken);
            }

            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.MethodNotAllowed,
                "Method not allowed.",
                _allowedOrigin,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process reseller credentials for {ResellerId}. Method: {Method}", resellerId, req.Method);
            return await FunctionHttp.CreateErrorResponseAsync(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to update reseller credentials.",
                _allowedOrigin,
                cancellationToken);
        }
    }

    private static class HttpMethods
    {
        public static bool IsGet(string method) =>
            string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);

        public static bool IsPost(string method) =>
            string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

        public static bool IsPut(string method) =>
            string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase);

        public static bool IsDelete(string method) =>
            string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
    }
}