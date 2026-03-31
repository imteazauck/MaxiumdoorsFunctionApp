using System.Text.Json.Serialization;

namespace MaxiumDoorsFunctionApp;

public sealed class CreateOrderResponseDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("orderNumber")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("quoteRef")]
    public string QuoteRef { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = "Order successfully created.";
}