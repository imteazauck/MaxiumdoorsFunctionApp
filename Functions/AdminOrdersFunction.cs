using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class AdminOrdersFunction
{
    private readonly CosmosOrderRepository _repository;
    private readonly AdminAuthService _adminAuthService;
    private readonly ILogger<AdminOrdersFunction> _logger;
    private readonly string _allowedOrigin;

    public AdminOrdersFunction(
        CosmosOrderRepository repository,
        AdminAuthService adminAuthService,
        ILogger<AdminOrdersFunction> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _adminAuthService = adminAuthService;
        _logger = logger;
        _allowedOrigin = configuration["AllowedOrigin"]
            ?? configuration["Values:AllowedOrigin"]
            ?? "http://localhost:5173";
    }

    [Function("AdminListOrders")]
    public async Task<HttpResponseData> ListOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "backoffice/orders")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        var principal = await FunctionHttp.ValidateAdminAsync(req, _adminAuthService, cancellationToken);
        if (principal is null)
        {
            return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Admin authentication is required.", _allowedOrigin, cancellationToken);
        }

        try
        {
            var query = QueryHelpers.ParseQuery(req.Url.Query);
            var status = query.TryGetValue("status", out var statusValue) ? statusValue.ToString() : null;
            var paymentStatus = query.TryGetValue("paymentStatus", out var paymentStatusValue) ? paymentStatusValue.ToString() : null;
            var search = query.TryGetValue("search", out var searchValue) ? searchValue.ToString() : null;

            var items = await _repository.GetOrdersAsync(status, paymentStatus, search, cancellationToken);
            return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, items, _allowedOrigin, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list admin orders.");
            return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Unable to load orders.", _allowedOrigin, cancellationToken);
        }
    }

    [Function("AdminGetOrder")]
    public async Task<HttpResponseData> GetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "backoffice/orders/{orderNumber}")] HttpRequestData req,
        string orderNumber,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        var principal = await FunctionHttp.ValidateAdminAsync(req, _adminAuthService, cancellationToken);
        if (principal is null)
        {
            return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Admin authentication is required.", _allowedOrigin, cancellationToken);
        }

        try
        {
            var order = await _repository.GetOrderByOrderNumberAsync(orderNumber, cancellationToken);
            if (order is null)
            {
                return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.NotFound, "Order not found.", _allowedOrigin, cancellationToken);
            }

            return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, order, _allowedOrigin, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin order {OrderNumber}.", orderNumber);
            return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Unable to load the selected order.", _allowedOrigin, cancellationToken);
        }
    }

    [Function("AdminTakePayment")]
    public async Task<HttpResponseData> TakePayment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "backoffice/orders/{orderNumber}/take-payment")] HttpRequestData req,
        string orderNumber,
        CancellationToken cancellationToken)
    {
        if (FunctionHttp.IsOptions(req.Method))
        {
            return FunctionHttp.CreateCorsResponse(req, HttpStatusCode.OK, _allowedOrigin);
        }

        var principal = await FunctionHttp.ValidateAdminAsync(req, _adminAuthService, cancellationToken);
        if (principal is null)
        {
            return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.Unauthorized, "Admin authentication is required.", _allowedOrigin, cancellationToken);
        }

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<ManualPaymentUpdateRequestDto>(req.Body, FunctionHttp.JsonOptions, cancellationToken)
                         ?? new ManualPaymentUpdateRequestDto();

            var updated = await _repository.TakePaymentAsync(orderNumber, principal.Identity?.Name ?? "admin", payload, cancellationToken);
            if (updated is null)
            {
                return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.NotFound, "Order not found.", _allowedOrigin, cancellationToken);
            }

            return await FunctionHttp.CreateJsonResponseAsync(req, HttpStatusCode.OK, updated, _allowedOrigin, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to take payment for order {OrderNumber}.", orderNumber);
            return await FunctionHttp.CreateErrorResponseAsync(req, HttpStatusCode.InternalServerError, "Unable to update payment.", _allowedOrigin, cancellationToken);
        }
    }
}
