namespace MaxiumDoorsFunctionApp;

public sealed class AdminLoginResponseDto
{
    public string token { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
    public string expiresAt { get; set; } = string.Empty;
}
