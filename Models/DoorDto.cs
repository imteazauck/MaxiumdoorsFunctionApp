namespace MaxiumDoorsFunctionApp;

public sealed class DoorDto
{
    public string DoorRef { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DoorSelectionsDto Selections { get; set; } = new();
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public List<string> TechnicalNotes { get; set; } = [];
}