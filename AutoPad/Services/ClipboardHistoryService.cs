using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using AutoPad.Models;

namespace AutoPad.Services;

public class ClipboardHistoryService
{
    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoPad", "history");

    private static readonly string HistoryFile = Path.Combine(HistoryDir, "history.json");

    private readonly List<ClipboardHistoryItem> _items = new();
    private int _maxItems = 50;

    public IReadOnlyList<ClipboardHistoryItem> Items => _items;

    public int MaxItems
    {
        get => _maxItems;
        set => _maxItems = Math.Max(10, value);
    }

    public void AddText(string text, long sizeBytes)
    {
        var preview = text.Length > 100 ? text[..100] + "..." : text;
        preview = preview.Replace("\r", "").Replace("\n", " ");

        var item = new ClipboardHistoryItem
        {
            Type = ClipboardHistoryItemType.Text,
            TextContent = text,
            Preview = preview,
            SizeBytes = sizeBytes
        };
        Add(item);
    }

    public void AddImage(BitmapSource image)
    {
        EnsureDirectory();
        var fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        var filePath = Path.Combine(HistoryDir, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(filePath);
        encoder.Save(stream);

        var item = new ClipboardHistoryItem
        {
            Type = ClipboardHistoryItemType.Image,
            ImagePath = filePath,
            Preview = $"{image.PixelWidth} x {image.PixelHeight} px",
            SizeBytes = new FileInfo(filePath).Length
        };
        Add(item);
    }

    public void AddFile(string filePath)
    {
        var item = new ClipboardHistoryItem
        {
            Type = ClipboardHistoryItemType.File,
            FilePath = filePath,
            Preview = Path.GetFileName(filePath),
            SizeBytes = File.Exists(filePath) ? new FileInfo(filePath).Length : 0
        };
        Add(item);
    }

    private void Add(ClipboardHistoryItem item)
    {
        // Remove duplicate if same content
        if (item.Type == ClipboardHistoryItemType.Text && item.TextContent != null)
        {
            _items.RemoveAll(x => x.Type == ClipboardHistoryItemType.Text && x.TextContent == item.TextContent);
        }
        else if (item.Type == ClipboardHistoryItemType.File && item.FilePath != null)
        {
            _items.RemoveAll(x => x.Type == ClipboardHistoryItemType.File && x.FilePath == item.FilePath);
        }

        _items.Insert(0, item);

        // Trim excess
        while (_items.Count > _maxItems)
        {
            var removed = _items[_items.Count - 1];
            // Clean up image file
            if (removed.ImagePath != null && File.Exists(removed.ImagePath))
            {
                try { File.Delete(removed.ImagePath); } catch { }
            }
            _items.RemoveAt(_items.Count - 1);
        }
    }

    public void Clear()
    {
        // Clean up image files
        foreach (var item in _items)
        {
            if (item.ImagePath != null && File.Exists(item.ImagePath))
            {
                try { File.Delete(item.ImagePath); } catch { }
            }
        }
        _items.Clear();
        Save();
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return;
            var json = File.ReadAllText(HistoryFile);
            var items = JsonSerializer.Deserialize<List<ClipboardHistoryItem>>(json);
            if (items != null)
            {
                _items.Clear();
                _items.AddRange(items);
            }
        }
        catch
        {
            // Load failed, start fresh
        }
    }

    public void Save()
    {
        try
        {
            EnsureDirectory();
            var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HistoryFile, json);
        }
        catch
        {
            // Save failed
        }
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(HistoryDir))
            Directory.CreateDirectory(HistoryDir);
    }
}
