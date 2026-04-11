using Newtonsoft.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class ResellerDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("resellerId")]
    public string ResellerId { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "reseller";

    [JsonProperty("companyName")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("businessAddress")]
    public string BusinessAddress { get; set; } = string.Empty;

    [JsonProperty("tel")]
    public string Tel { get; set; } = string.Empty;

    [JsonProperty("fax")]
    public string Fax { get; set; } = string.Empty;

    [JsonProperty("mobile")]
    public string Mobile { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("webAddress")]
    public string WebAddress { get; set; } = string.Empty;

    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonProperty("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("pricingInitialized")]
    public bool PricingInitialized { get; set; }

    [JsonProperty("sourceTemplateId")]
    public string SourceTemplateId { get; set; } = "default";

    [JsonProperty("credentials")]
    public ResellerCredentialSummaryDto Credentials { get; set; } = new();

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonProperty("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}
