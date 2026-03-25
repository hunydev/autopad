using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WpfClipboard = System.Windows.Clipboard;

namespace AutoPad.Services;

/// <summary>
/// Windows 클립보드 변경을 모니터링하는 서비스
/// </summary>
public class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private HwndSource? _hwndSource;
    private bool _isMonitoring;

    /// <summary>
    /// 파일 모니터링 최대 크기 (바이트)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 기본 10MB

    /// <summary>
    /// 파일 모니터링 활성화 여부
    /// </summary>
    public bool IsFileMonitoringEnabled { get; set; } = true;

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public void Start(Window window)
    {
        if (_isMonitoring) return;

        var helper = new WindowInteropHelper(window);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);

        AddClipboardFormatListener(helper.Handle);
        _isMonitoring = true;
    }

    public void Stop()
    {
        if (!_isMonitoring || _hwndSource == null) return;

        RemoveClipboardFormatListener(_hwndSource.Handle);
        _hwndSource.RemoveHook(WndProc);
        _isMonitoring = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            OnClipboardChanged();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnClipboardChanged()
    {
        try
        {
            // 이미지를 먼저 확인 (이미지 복사 시 텍스트도 포함될 수 있음)
            if (WpfClipboard.ContainsImage())
            {
                var image = WpfClipboard.GetImage();
                if (image != null)
                {
                    ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(ClipboardContentType.Image, image));
                    return;
                }
            }
            
            if (WpfClipboard.ContainsText())
            {
                var text = WpfClipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    // HTML 포맷 데이터도 함께 확인 (UTF-8 바이트로 직접 디코딩)
                    string? htmlContent = null;
                    if (WpfClipboard.ContainsData(System.Windows.DataFormats.Html))
                    {
                        var dataObj = WpfClipboard.GetDataObject();
                        if (dataObj != null)
                        {
                            var rawData = dataObj.GetData(System.Windows.DataFormats.Html, false);
                            if (rawData is System.IO.MemoryStream ms)
                            {
                                htmlContent = System.Text.Encoding.UTF8.GetString(ms.ToArray()).TrimEnd('\0');
                            }
                            else if (rawData is string s)
                            {
                                htmlContent = s;
                            }
                        }
                    }
                    ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(ClipboardContentType.Text, text, htmlContent));
                    return;
                }
            }
            
            if (IsFileMonitoringEnabled && WpfClipboard.ContainsFileDropList())
            {
                var files = WpfClipboard.GetFileDropList();
                if (files != null && files.Count > 0)
                {
                    // 첫 번째 파일만 처리 (단일 파일 기준)
                    var filePath = files[0];
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length <= MaxFileSizeBytes)
                        {
                            ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(ClipboardContentType.File, filePath));
                        }
                    }
                }
            }
        }
        catch
        {
            // 클립보드 접근 실패 시 무시
        }
    }

    public void Dispose()
    {
        Stop();
        _hwndSource?.Dispose();
    }
}

public enum ClipboardContentType
{
    Text,
    Image,
    File
}

public class ClipboardChangedEventArgs : EventArgs
{
    public ClipboardContentType ContentType { get; }
    public object Content { get; }
    public string? HtmlContent { get; }

    public ClipboardChangedEventArgs(ClipboardContentType type, object content, string? htmlContent = null)
    {
        ContentType = type;
        Content = content;
        HtmlContent = htmlContent;
    }
}
