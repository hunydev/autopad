using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AutoPad.Services;

/// <summary>
/// 앱 아이콘을 리소스에서 로드하는 헬퍼 클래스
/// </summary>
public static class IconHelper
{
    private static Bitmap LoadIconBitmap(int size)
    {
        var uri = new Uri("pack://application:,,,/Resources/icon.png");
        var stream = System.Windows.Application.GetResourceStream(uri)?.Stream
            ?? throw new InvalidOperationException("icon.png resource not found");
        
        using var original = new Bitmap(stream);
        var resized = new Bitmap(size, size);
        using var g = Graphics.FromImage(resized);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(original, 0, 0, size, size);
        return resized;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreateAppIcon(int size = 32)
    {
        using var bitmap = LoadIconBitmap(size);
        var hIcon = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    public static BitmapSource CreateAppIconImageSource(int size = 32)
    {
        using var bitmap = LoadIconBitmap(size);
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
}
