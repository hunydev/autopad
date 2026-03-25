using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace AutoPad.Services;

/// <summary>
/// 테마 관련 헬퍼 (다크/라이트 모드, 제목 표시줄 색상)
/// </summary>
public static class ThemeHelper
{
    // DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

    /// <summary>
    /// 윈도우에 다크 모드 제목 표시줄 적용
    /// </summary>
    public static void ApplyDarkTitleBar(Window window, bool isDarkMode)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        int useImmersiveDarkMode = isDarkMode ? 1 : 0;
        
        // Windows 10 20H1 이상
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int)) != 0)
        {
            // Windows 10 20H1 이전 버전
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useImmersiveDarkMode, sizeof(int));
        }
    }

    /// <summary>
    /// SourceInitialized 이벤트 핸들러로 사용할 수 있는 메서드
    /// </summary>
    public static void ApplyDarkTitleBarOnInit(Window window, bool isDarkMode)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            // 핸들이 아직 없으면 SourceInitialized에서 다시 호출
            window.SourceInitialized += (s, e) => ApplyDarkTitleBar(window, isDarkMode);
        }
        else
        {
            ApplyDarkTitleBar(window, isDarkMode);
        }
    }

    // 테마 색상 정의
    public static class DarkTheme
    {
        public static WpfColor Background => WpfColor.FromRgb(0x1E, 0x1E, 0x1E);
        public static WpfColor SecondaryBackground => WpfColor.FromRgb(0x2D, 0x2D, 0x30);
        public static WpfColor BorderColor => WpfColor.FromRgb(0x3F, 0x3F, 0x46);
        public static WpfColor TextColor => WpfColor.FromRgb(0xE0, 0xE0, 0xE0);
        public static WpfColor SecondaryTextColor => WpfColor.FromRgb(0xAA, 0xAA, 0xAA);
        public static WpfColor AccentColor => WpfColor.FromRgb(0x00, 0x78, 0xD4);
        public static WpfColor HoverColor => WpfColor.FromRgb(0x50, 0x50, 0x50);
        
        public static SolidColorBrush BackgroundBrush => new(Background);
        public static SolidColorBrush SecondaryBackgroundBrush => new(SecondaryBackground);
        public static SolidColorBrush BorderBrush => new(BorderColor);
        public static SolidColorBrush TextBrush => new(TextColor);
        public static SolidColorBrush SecondaryTextBrush => new(SecondaryTextColor);
        public static SolidColorBrush AccentBrush => new(AccentColor);
    }

    public static class LightTheme
    {
        public static WpfColor Background => WpfColor.FromRgb(0xF5, 0xF5, 0xF5);
        public static WpfColor SecondaryBackground => WpfColor.FromRgb(0xFF, 0xFF, 0xFF);
        public static WpfColor BorderColor => WpfColor.FromRgb(0xD0, 0xD0, 0xD0);
        public static WpfColor TextColor => WpfColor.FromRgb(0x1E, 0x1E, 0x1E);
        public static WpfColor SecondaryTextColor => WpfColor.FromRgb(0x60, 0x60, 0x60);
        public static WpfColor AccentColor => WpfColor.FromRgb(0x00, 0x78, 0xD4);
        public static WpfColor HoverColor => WpfColor.FromRgb(0xE8, 0xE8, 0xE8);
        
        public static SolidColorBrush BackgroundBrush => new(Background);
        public static SolidColorBrush SecondaryBackgroundBrush => new(SecondaryBackground);
        public static SolidColorBrush BorderBrush => new(BorderColor);
        public static SolidColorBrush TextBrush => new(TextColor);
        public static SolidColorBrush SecondaryTextBrush => new(SecondaryTextColor);
        public static SolidColorBrush AccentBrush => new(AccentColor);
    }
}
