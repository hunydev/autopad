# AutoPad

A lightweight Windows clipboard monitoring utility that detects copied content in real-time, shows toast notifications, and provides instant editing — no separate text editor needed.

## Features

- **Clipboard Monitoring** — Automatically detects text, images, and file copies
- **Toast Notifications** — Non-intrusive popup with preview, character count, and byte size
- **Instant Editing** — Edit copied text or annotate images directly from the toast popup
- **Image Markup** — Pen, eraser (stroke/point), region selection with mosaic, fill, and erase tools
- **HTML Source Viewer** — View raw HTML source when web content is copied with formatting
- **Path Detection** — Detects file/folder paths in clipboard text with quick-open buttons
- **File Monitoring** — Detects file copy operations with thumbnail preview for images
- **Encoding Support** — UTF-8 and EUC-KR encoding conversion for text files
- **Multi-language** — English (default) and Korean UI
- **System Tray** — Runs quietly in the background with tray icon
- **Dark Theme** — Full dark mode UI including title bar and scrollbars
- **Auto-start** — Optional Windows startup registration

## Screenshots

*Coming soon*

## Requirements

- Windows 10/11
- .NET 8.0 Runtime (or use self-contained build)

## Installation

### Option 1: Download Release

Download the latest release from [Releases](../../releases).

### Option 2: Build from Source

```powershell
git clone https://github.com/hunydev/autopad.git
cd autopad/AutoPad
dotnet build
dotnet run
```

### Publishing

```powershell
# Self-contained (no .NET runtime required)
dotnet publish -c Release -r win-x64 --self-contained true

# Single-file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Or use the included build script:

```powershell
./publish.bat
```

## Usage

1. Launch AutoPad — it appears as a tray icon in the system tray
2. Copy any text, image, or file anywhere on your system
3. A toast notification appears with a preview
4. Click **Edit** to open the editing window, or **HTML Source** to view raw HTML (when available)
5. Edit content and click **Copy & Close** to save back to clipboard

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Z` | Undo (image drawing / selection operations) |

## Configuration

Double-click the tray icon or right-click → Settings:

| Option | Description |
|--------|-------------|
| Language | English / 한국어 |
| Toast Position | Bottom/Top Center, Bottom/Top Left/Right |
| Notification Duration | 3, 5, 10, or 15 seconds |
| Clipboard Monitoring | Enable/disable monitoring |
| File Copy Monitoring | Enable/disable with size limit (1–100 MB) |
| Auto-start | Register with Windows startup |
| Start Minimized | Launch directly to system tray |

## Tech Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 8.0 |
| UI Framework | WPF (Windows Presentation Foundation) |
| System Tray | WinForms NotifyIcon |
| Platform | Windows 10/11 (`net8.0-windows`) |
| Icons | Segoe Fluent Icons |

### Key Technical Details

- **Win32 Interop** — `AddClipboardFormatListener` for clipboard change detection, `DwmSetWindowAttribute` for dark title bar
- **InkCanvas** — Image drawing with pen, eraser (stroke/point modes), and brush size control
- **BitmapSource pixel manipulation** — Mosaic, solid fill, and transparent erase on selected regions
- **CF_HTML parsing** — Raw clipboard HTML format extraction with UTF-8 byte offset handling
- **Mutex** — Single instance enforcement

## Project Structure

```
AutoPad/
├── App.xaml.cs              # Application entry, global clipboard event routing
├── Services/
│   ├── ClipboardMonitor.cs  # Win32 clipboard change listener
│   ├── Localization.cs      # Multi-language string resources (en/ko)
│   ├── SettingsService.cs   # JSON settings persistence
│   ├── ThemeHelper.cs       # Dark mode DWM API
│   └── IconHelper.cs        # Dynamic app icon generation
├── Models/
│   └── AppSettings.cs       # Settings data model
└── Windows/
    ├── ToastWindow.xaml      # Toast notification popup
    ├── EditWindow.xaml       # Text/image editing window
    ├── HtmlViewerWindow.xaml # HTML source viewer (read-only)
    └── SettingsWindow.xaml   # Settings dialog
```

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
- WPF (Windows Presentation Foundation)
- C# 12

## 시스템 요구사항

- Windows 10 / 11
- .NET 8.0 Runtime (또는 self-contained 배포 시 불필요)

## 라이선스

MIT License
