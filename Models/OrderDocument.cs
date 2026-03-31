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

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonProperty("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}