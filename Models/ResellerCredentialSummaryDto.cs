using System.Text.Json.Serialization;

namespace MaxiumDoorsFunctionApp;

public sealed class ResellerCredentialSummaryDto
{
    public bool LoginEnabled { get; set; }
    public string LoginEmail { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public string? PasswordLastSetAt { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PasswordHash { get; set; }
}
