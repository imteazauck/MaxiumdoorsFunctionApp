namespace MaxiumDoorsFunctionApp;

public sealed class ManualPaymentUpdateRequestDto
{
    public string PaymentMethod { get; set; } = "card";
    public string PaymentReference { get; set; } = string.Empty;
    public decimal? AmountPaid { get; set; }
    public string? PaidAt { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool CompleteOrder { get; set; } = true;
}
