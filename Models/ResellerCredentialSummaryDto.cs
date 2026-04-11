namespace MaxiumDoorsFunctionApp;

public sealed class ResellerCredentialSummaryDto
{
    public bool LoginEnabled { get; set; }
    public string LoginEmail { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public string? PasswordLastSetAt { get; set; }

    // Temporary development-only placeholder until password hashing is implemented.
    public string? PasswordPlaintext { get; set; }
}
