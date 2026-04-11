using Newtonsoft.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class AuthUser
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("resellerId")]
    public string? ResellerId { get; set; }

    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
