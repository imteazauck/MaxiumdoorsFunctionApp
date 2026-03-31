namespace MaxiumDoorsFunctionApp;

public sealed class CreateOrderRequestDto
{
    public string QuoteRef { get; set; } = string.Empty;
    public CustomerDetailsDto CustomerDetails { get; set; } = new();
    public List<DoorDto> Doors { get; set; } = [];
    public decimal Subtotal { get; set; }
    public DeliveryDetailsDto DeliveryDetails { get; set; } = new();
    public PaymentDto Payment { get; set; } = new();
}