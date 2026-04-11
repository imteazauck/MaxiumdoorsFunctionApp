namespace MaxiumDoorsFunctionApp;

public sealed class ResellerCredentialUpdateRequestDto
{
    public string ResellerId { get; set; } = string.Empty;
    public bool LoginEnabled { get; set; }
    public string LoginEmail { get; set; } = string.Empty;
    public string? Password { get; set; }
}
