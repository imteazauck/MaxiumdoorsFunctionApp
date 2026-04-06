using Newtonsoft.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class OrderDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    [JsonProperty("orderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "order";

    [JsonProperty("quoteRef")]
    public string QuoteRef { get; set; } = string.Empty;

    [JsonProperty("customerDetails")]
    public CustomerDetailsDto CustomerDetails { get; set; } = new();

    [JsonProperty("doors")]
    public List<DoorDto> Doors { get; set; } = [];

    [JsonProperty("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonProperty("deliveryDetails")]
    public DeliveryDetailsDto DeliveryDetails { get; set; } = new();

    [JsonProperty("payment")]
    public PaymentDto Payment { get; set; } = new();

    [JsonProperty("orderStatus")]
    public string OrderStatus { get; set; } = "pending";

    [JsonProperty("paymentStatus")]
    public string PaymentStatus { get; set; } = "unpaid";

    [JsonProperty("paymentMethod")]
    public string PaymentMethod { get; set; } = string.Empty;

    [JsonProperty("paymentReference")]
    public string PaymentReference { get; set; } = string.Empty;

    [JsonProperty("amountPaid")]
    public decimal? AmountPaid { get; set; }

    [JsonProperty("paidAt")]
    public string? PaidAt { get; set; }

    [JsonProperty("completedAt")]
    public string? CompletedAt { get; set; }

    [JsonProperty("adminNotes")]
    public string AdminNotes { get; set; } = string.Empty;

    [JsonProperty("lastUpdatedBy")]
    public string LastUpdatedBy { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonProperty("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}
