namespace MaxiumDoorsFunctionApp;

public sealed class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public AuthUserDto User { get; set; } = new();
}

public sealed class AuthUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? ResellerId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}