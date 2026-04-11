namespace MaxiumDoorsFunctionApp;

public sealed class PricingTemplateSeedRow
{
    public string Type { get; set; } = string.Empty;
    public string SourceTemplateId { get; set; } = "default";
    public string? DoorCategory { get; set; }
    public string? Group { get; set; }
    public string? Label { get; set; }
    public string Configuration { get; set; } = "single";
    public int? HeightMin { get; set; }
    public int? HeightMax { get; set; }
    public int? WidthMin { get; set; }
    public int? WidthMax { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "GBP";
}
