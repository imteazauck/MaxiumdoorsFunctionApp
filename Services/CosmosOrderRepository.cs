using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

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
            QuoteRef = payload.QuoteRef,
            CustomerDetails = payload.CustomerDetails,
            Doors = payload.Doors,
            Subtotal = payload.Subtotal,
            DeliveryDetails = payload.DeliveryDetails,
            Payment = payload.Payment,
            OrderStatus = "pending",
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
}
