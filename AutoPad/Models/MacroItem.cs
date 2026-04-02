namespace AutoPad.Models;

public class MacroItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Script { get; set; } = "function transform(input) {\n    return input;\n}";
}
