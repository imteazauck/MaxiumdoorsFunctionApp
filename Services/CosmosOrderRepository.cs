using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace MaxiumDoorsFunctionApp;

public sealed class CosmosOrderRepository
{
    private readonly Container _container;

    public CosmosOrderRepository(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseId = configuration["CosmosDb:DatabaseId"]
            ?? throw new InvalidOperationException("CosmosDb:DatabaseId is missing.");

        var containerId = configuration["CosmosDb:ContainerId"]
            ?? throw new InvalidOperationException("CosmosDb:ContainerId is missing.");

        _container = cosmosClient.GetContainer(databaseId, containerId);
    }

    public async Task<CreateOrderResponseDto> CreateOrderAsync(
        CreateOrderRequestDto payload,
        string? resellerId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var orderNumber = $"ORD-{now:yyyyMMdd-HHmmss}-{Random.Shared.Next(1000, 9999)}";

        var document = new OrderDocument
        {
            Id = Guid.NewGuid().ToString(),
            PartitionKey = orderNumber,
            OrderNumber = orderNumber,
            QuoteRef = payload.QuoteRef?.Trim() ?? string.Empty,
            ResellerId = string.IsNullOrWhiteSpace(resellerId) ? null : resellerId.Trim(),
            CustomerDetails = SanitizeCustomerDetails(payload.CustomerDetails),
            Doors = payload.Doors ?? [],
            Subtotal = payload.Subtotal,
            DeliveryDetails = SanitizeDeliveryDetails(payload.DeliveryDetails),
            Payment = SanitizePayment(payload.Payment),
            OrderStatus = "pending",
            PaymentStatus = "unpaid",
            CreatedAt = now.ToString("O"),
            UpdatedAt = now.ToString("O")
        };

        try
        {
            await _container.CreateItemAsync(document, new PartitionKey(document.PartitionKey), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            throw new InvalidOperationException($"Cosmos create failed. StatusCode={(int)ex.StatusCode}. Message={ex.Message}", ex);
        }

        return new CreateOrderResponseDto
        {
            Id = document.Id,
            OrderNumber = document.OrderNumber,
            Status = document.OrderStatus,
            CreatedAt = document.CreatedAt,
            QuoteRef = document.QuoteRef,
            Message = "Order successfully created."
        };
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetOrdersAsync(
        string? status,
        string? paymentStatus,
        string? search,
        string? resellerId = null,
        CancellationToken cancellationToken = default)
    {
        var sql = new StringBuilder(@"
            SELECT
                c.id,
                c.orderNumber,
                c.quoteRef,
                c.orderStatus,
                c.paymentStatus,
                c.subtotal,
                c.customerDetails.CustomerName AS customerName,
                c.customerDetails.CompanyName AS companyName,
                c.customerDetails.Email AS customerEmail,
                ARRAY_LENGTH(c.doors) AS doorCount,
                c.createdAt,
                c.updatedAt
            FROM c
            WHERE c.type = 'order'");

        if (!string.IsNullOrWhiteSpace(resellerId))
        {
            sql.Append(" AND c.resellerId = @resellerId");
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            sql.Append(" AND LOWER(c.orderStatus) = LOWER(@status)");
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus) && !string.Equals(paymentStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            sql.Append(" AND LOWER(c.paymentStatus) = LOWER(@paymentStatus)");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql.Append(@"
                AND (
                    CONTAINS(LOWER(c.orderNumber), LOWER(@search))
                    OR CONTAINS(LOWER(c.quoteRef), LOWER(@search))
                    OR CONTAINS(LOWER(c.customerDetails.CustomerName), LOWER(@search))
                    OR CONTAINS(LOWER(c.customerDetails.CompanyName), LOWER(@search))
                    OR CONTAINS(LOWER(c.customerDetails.Email), LOWER(@search))
                )");
        }

        sql.Append(" ORDER BY c.createdAt DESC");

        var queryDefinition = new QueryDefinition(sql.ToString());
        if (!string.IsNullOrWhiteSpace(resellerId)) queryDefinition.WithParameter("@resellerId", resellerId);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)) queryDefinition.WithParameter("@status", status);
        if (!string.IsNullOrWhiteSpace(paymentStatus) && !string.Equals(paymentStatus, "all", StringComparison.OrdinalIgnoreCase)) queryDefinition.WithParameter("@paymentStatus", paymentStatus);
        if (!string.IsNullOrWhiteSpace(search)) queryDefinition.WithParameter("@search", search);

        var iterator = _container.GetItemQueryIterator<OrderSummaryDto>(queryDefinition);
        var results = new List<OrderSummaryDto>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response.Resource);
        }

        return results;
    }

    public async Task<OrderDocument?> GetOrderByOrderNumberAsync(
        string orderNumber,
        string? resellerId = null,
        CancellationToken cancellationToken = default)
    {
        var queryText = @"
            SELECT *
            FROM c
            WHERE c.type = 'order'
              AND c.orderNumber = @orderNumber" + (!string.IsNullOrWhiteSpace(resellerId) ? " AND c.resellerId = @resellerId" : string.Empty);

        var queryDefinition = new QueryDefinition(queryText).WithParameter("@orderNumber", orderNumber);
        if (!string.IsNullOrWhiteSpace(resellerId)) queryDefinition.WithParameter("@resellerId", resellerId);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(orderNumber) };
        var iterator = _container.GetItemQueryIterator<OrderDocument>(queryDefinition, requestOptions: options);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var order = response.Resource.FirstOrDefault();
            if (order is not null)
            {
                order.CustomerDetails = SanitizeCustomerDetails(order.CustomerDetails);
                order.DeliveryDetails = SanitizeDeliveryDetails(order.DeliveryDetails);
                order.Payment = SanitizePayment(order.Payment);
                order.Doors ??= [];
                return order;
            }
        }

        return null;
    }

    public async Task<OrderDocument?> TakePaymentAsync(
        string orderNumber,
        string updatedBy,
        ManualPaymentUpdateRequestDto payload,
        string? resellerId = null,
        CancellationToken cancellationToken = default)
    {
        var order = await GetOrderByOrderNumberAsync(orderNumber, resellerId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        order.PaymentStatus = "paid";
        order.PaymentMethod = string.IsNullOrWhiteSpace(payload.PaymentMethod) ? "card" : payload.PaymentMethod.Trim();
        order.PaymentReference = payload.PaymentReference?.Trim() ?? string.Empty;
        order.AmountPaid = payload.AmountPaid ?? order.Subtotal;
        order.PaidAt = ParseOrNow(payload.PaidAt, now).ToString("O");
        order.LastUpdatedBy = updatedBy;
        order.AdminNotes = AppendNote(order.AdminNotes, payload.Notes, updatedBy, now);
        order.UpdatedAt = now.ToString("O");

        if (payload.CompleteOrder)
        {
            order.OrderStatus = "completed";
            order.CompletedAt = now.ToString("O");
        }

        order.CustomerDetails = SanitizeCustomerDetails(order.CustomerDetails);
        order.DeliveryDetails = SanitizeDeliveryDetails(order.DeliveryDetails);
        order.Payment = SanitizePayment(order.Payment);

        await _container.ReplaceItemAsync(order, order.Id, new PartitionKey(order.PartitionKey), cancellationToken: cancellationToken);
        return order;
    }

    private static DateTime ParseOrNow(string? value, DateTime fallback)
        => DateTime.TryParse(value, out var parsed) ? parsed : fallback;

    private static string AppendNote(string existing, string? notes, string updatedBy, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return existing ?? string.Empty;
        }

        var entry = $"[{now:yyyy-MM-dd HH:mm}] {updatedBy}: {notes.Trim()}";
        return string.IsNullOrWhiteSpace(existing) ? entry : $"{existing}{Environment.NewLine}{entry}";
    }

    private static CustomerDetailsDto SanitizeCustomerDetails(CustomerDetailsDto? customerDetails)
    {
        customerDetails ??= new CustomerDetailsDto();
        customerDetails.CustomerName ??= string.Empty;
        customerDetails.CompanyName ??= string.Empty;
        customerDetails.Email ??= string.Empty;
        customerDetails.Phone ??= string.Empty;
        customerDetails.AddressLine1 ??= string.Empty;
        customerDetails.AddressLine2 ??= string.Empty;
        customerDetails.City ??= string.Empty;
        customerDetails.Postcode ??= string.Empty;
        return customerDetails;
    }

    private static DeliveryDetailsDto SanitizeDeliveryDetails(DeliveryDetailsDto? deliveryDetails)
    {
        deliveryDetails ??= new DeliveryDetailsDto();
        deliveryDetails.AddressLine1 ??= string.Empty;
        deliveryDetails.AddressLine2 ??= string.Empty;
        deliveryDetails.City ??= string.Empty;
        deliveryDetails.County ??= string.Empty;
        deliveryDetails.Country ??= string.Empty;
        deliveryDetails.PostCode ??= string.Empty;
        deliveryDetails.ContactEmail ??= string.Empty;
        deliveryDetails.ContactPhone ??= string.Empty;
        deliveryDetails.SiteContactName ??= string.Empty;
        deliveryDetails.SiteContactPhone ??= string.Empty;
        deliveryDetails.DeliveryMethod ??= string.Empty;
        deliveryDetails.EstimatedDeliveryDate ??= string.Empty;
        return deliveryDetails;
    }

    private static PaymentDto SanitizePayment(PaymentDto? payment)
    {
        payment ??= new PaymentDto();
        payment.CardholderName ??= string.Empty;
        payment.CardNumber ??= string.Empty;
        payment.Expiry ??= string.Empty;
        payment.Cvv ??= string.Empty;
        return payment;
    }
}
