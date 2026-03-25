using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AutoPad.Services;

/// <summary>
/// 앱 아이콘을 동적으로 생성하는 헬퍼 클래스
/// </summary>
public static class IconHelper
{
    public static Icon CreateAppIcon(int size = 32)
    {
        using var bitmap = CreateAppBitmap(size);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public static BitmapSource CreateAppIconImageSource(int size = 32)
    {
        using var bitmap = CreateAppBitmap(size);
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static Bitmap CreateAppBitmap(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        
        // 배경 원형
        using var gradientBrush = new LinearGradientBrush(
            new Rectangle(0, 0, size, size),
            Color.FromArgb(0, 120, 212),  // #0078D4
            Color.FromArgb(0, 188, 242),  // #00BCF2
            LinearGradientMode.ForwardDiagonal);
        
        graphics.FillEllipse(gradientBrush, 1, 1, size - 2, size - 2);
        
        // 클립보드 아이콘 (간단한 사각형 + 클립)
        int padding = size / 5;
        int clipboardWidth = size - padding * 2;
        int clipboardHeight = (int)(clipboardWidth * 1.2);
        int clipboardX = padding;
        int clipboardY = padding + 2;
        
        using var whiteBrush = new SolidBrush(Color.White);
        using var whitePen = new Pen(Color.White, Math.Max(1, size / 16f));
        
        // 클립보드 본체
        var clipboardRect = new Rectangle(clipboardX, clipboardY, clipboardWidth, clipboardHeight);
        graphics.FillRectangle(whiteBrush, clipboardRect);
        
        // 클립 (상단)
        int clipWidth = clipboardWidth / 2;
        int clipHeight = size / 6;
        int clipX = clipboardX + (clipboardWidth - clipWidth) / 2;
        int clipY = clipboardY - clipHeight / 2;
        
        using var accentBrush = new SolidBrush(Color.FromArgb(0, 120, 212));
        graphics.FillRectangle(accentBrush, clipX, clipY, clipWidth, clipHeight);
        
        // 텍스트 라인
        int lineY = clipboardY + clipHeight + 2;
        int lineHeight = Math.Max(2, size / 12);
        int lineSpacing = lineHeight + 2;
        
        using var lineBrush = new SolidBrush(Color.FromArgb(150, 0, 120, 212));
        for (int i = 0; i < 3 && lineY + lineHeight < clipboardY + clipboardHeight - 2; i++)
        {
            int lineWidth = clipboardWidth - 6 - (i % 2 == 1 ? 4 : 0);
            graphics.FillRectangle(lineBrush, clipboardX + 3, lineY, lineWidth, lineHeight);
            lineY += lineSpacing;
        }
        
        return bitmap;
    }
}
