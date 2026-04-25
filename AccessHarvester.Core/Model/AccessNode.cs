namespace AccessHarvester.Core.Model;

public class AccessNode
{
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<AccessNode> Children { get; set; } = new();
    public string? VbaCode { get; set; }
}
