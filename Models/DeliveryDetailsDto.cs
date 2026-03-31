namespace MaxiumDoorsFunctionApp;

public sealed class DeliveryDetailsDto
{
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? County { get; set; }
    public string Country { get; set; } = string.Empty;
    public bool UseParentsPostCode { get; set; }
    public string PostCode { get; set; } = string.Empty;
    public bool ConfirmAddress { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string SiteContactName { get; set; } = string.Empty;
    public string? SiteContactPhone { get; set; }
    public bool AmDelivery { get; set; }
    public bool Pre10AmDelivery { get; set; }
    public bool OffloadingAvailable { get; set; }
    public string DeliveryMethod { get; set; } = string.Empty;
    public string EstimatedDeliveryDate { get; set; } = string.Empty;
}