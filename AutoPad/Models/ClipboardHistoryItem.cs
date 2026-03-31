using System.Text.Json.Serialization;

namespace AutoPad.Models;

public class ClipboardHistoryItem
{
    public ClipboardHistoryItemType Type { get; set; }
    public string? TextContent { get; set; }
    public string? ImagePath { get; set; }
    public string? FilePath { get; set; }
    public string? Preview { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public long SizeBytes { get; set; }

    [JsonIgnore]
    public string DisplayTime => Timestamp.ToString("HH:mm:ss");

    [JsonIgnore]
    public string TypeIcon => Type switch
    {
        ClipboardHistoryItemType.Text => "\uE8C1",   // Document
        ClipboardHistoryItemType.Image => "\uEB9F",   // Photo
        ClipboardHistoryItemType.File => "\uE7C3",    // Attach
        _ => "\uE7C3"
    };
}

public enum ClipboardHistoryItemType
{
    Text,
    Image,
    File
}
