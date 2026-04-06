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
            await _container.CreateItemAsync(
                document,
                new PartitionKey(document.PartitionKey),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            throw new InvalidOperationException(
                $"Cosmos create failed. StatusCode={(int)ex.StatusCode}. Message={ex.Message}",
                ex);
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

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            queryDefinition.WithParameter("@status", status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus) && !string.Equals(paymentStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            queryDefinition.WithParameter("@paymentStatus", paymentStatus.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            queryDefinition.WithParameter("@search", search.Trim());
        }

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
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(@"
            SELECT TOP 1 *
            FROM c
            WHERE c.type = 'order'
              AND c.orderNumber = @orderNumber")
            .WithParameter("@orderNumber", orderNumber);

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(orderNumber)
        };

        var iterator = _container.GetItemQueryIterator<OrderDocument>(
            queryDefinition: query,
            requestOptions: requestOptions);

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
        CancellationToken cancellationToken = default)
    {
        var order = await GetOrderByOrderNumberAsync(orderNumber, cancellationToken);
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

        var response = await _container.ReplaceItemAsync(
            order,
            order.Id,
            new PartitionKey(order.PartitionKey),
            cancellationToken: cancellationToken);

        return response.Resource;
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

        var digits = new string((payment.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits[^4..] : digits;

        return new PaymentDto
        {
            CardholderName = payment.CardholderName ?? string.Empty,
            CardNumber = string.IsNullOrWhiteSpace(last4) ? string.Empty : $"**** **** **** {last4}",
            Expiry = payment.Expiry ?? string.Empty,
            Cvv = string.Empty,
            Last4 = last4
        };
    }

    private static DateTime ParseOrNow(string? value, DateTime fallback)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed.ToUniversalTime() : fallback;
    }

    private static string AppendNote(string existing, string incoming, string updatedBy, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return existing;
        }

        var note = $"[{timestamp:yyyy-MM-dd HH:mm:ss} UTC by {updatedBy}] {incoming.Trim()}";
        return string.IsNullOrWhiteSpace(existing) ? note : $"{existing}\n{note}";
    }
}