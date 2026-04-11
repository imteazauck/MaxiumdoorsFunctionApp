using Newtonsoft.Json;

namespace MaxiumDoorsFunctionApp;

public sealed class ResellerPricingDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("resellerId")]
    public string ResellerId { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("sourceTemplateId")]
    public string SourceTemplateId { get; set; } = "default";

    [JsonProperty("doorCategory")]
    public string? DoorCategory { get; set; }

    [JsonProperty("group")]
    public string? Group { get; set; }

    [JsonProperty("label")]
    public string? Label { get; set; }

    [JsonProperty("configuration")]
    public string Configuration { get; set; } = "single";

    [JsonProperty("heightMin")]
    public int? HeightMin { get; set; }

    [JsonProperty("heightMax")]
    public int? HeightMax { get; set; }

    [JsonProperty("widthMin")]
    public int? WidthMin { get; set; }

    [JsonProperty("widthMax")]
    public int? WidthMax { get; set; }

    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("currency")]
    public string Currency { get; set; } = "GBP";

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonProperty("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}
