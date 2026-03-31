namespace MaxiumDoorsFunctionApp;

public sealed class PaymentDto
{
    public string CardholderName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
}