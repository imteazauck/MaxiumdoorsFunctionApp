using System.Text.Json.Serialization;

namespace MaxiumDoorsFunctionApp;

public sealed class AuthUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("email")]
    public string Email { get; set; } = default!;

    [JsonPropertyName("passwordHash")]
    public string PasswordHash { get; set; } = default!;

    [JsonPropertyName("role")]
    public string Role { get; set; } = default!;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("resellerId")]
    public string? ResellerId { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = default!;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}