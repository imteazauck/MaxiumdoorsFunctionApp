namespace MaxiumDoorsFunctionApp;

public sealed class OrderConfirmationDto
{
    public string OrderNumber { get; set; } = string.Empty;
    public string QuoteRef { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}