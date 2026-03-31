using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AutoPad.Models;
using AutoPad.Services;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace AutoPad.Windows;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly string? _textContent;
    private readonly BitmapSource? _imageContent;
    private readonly string? _filePath;
    private readonly string? _detectedPath;
    private readonly string? _detectedUrl;
    private readonly string? _htmlContent;
    private readonly ToastPosition _position;
    private double _targetOpacity;

    public event EventHandler? EditRequested;

    public ToastWindow(string text, ToastPosition position, int durationSeconds, bool isCompact = false, string? htmlContent = null)
    {
        InitializeComponent();
        _targetOpacity = (App.SettingsService?.Settings.ToastOpacityPercent ?? 100) / 100.0;
        _textContent = text;
        _htmlContent = htmlContent;
        _position = position;

        // 버튼 텍스트 다국어 적용
        SaveButton.Content = Loc.BtnSave;
        EditButton.Content = Loc.BtnEdit;
        HtmlViewButton.Content = Loc.BtnHtmlSource;
        FileEditButton.Content = Loc.BtnFileEdit;
        FileOpenButton.Content = Loc.BtnFileOpen;
        FolderOpenButton.Content = Loc.BtnFolderOpen;
        UrlOpenButton.Content = Loc.BtnUrlOpen;

        var byteSize = Encoding.UTF8.GetByteCount(text);
        var runeCount = text.EnumerateRunes().Count();
        var lineCount = text.Split('\n').Length;
        var sizeText = Loc.FormatSize(byteSize);
        
        MessageText.Text = Loc.TextCopied(runeCount, sizeText, lineCount);
        
        var preview = text.Length > 60 ? text[..60] + "..." : text;
        preview = preview.Replace("\r", "").Replace("\n", " ");
        PreviewText.Text = preview;

        // 경로 감지: 텍스트가 파일 또는 폴더 경로인지 확인
        var trimmed = text.Trim().Trim('"');
        if (!trimmed.Contains('\n') && !trimmed.Contains('\r'))
        {
            try
            {
                if (File.Exists(trimmed))
                {
                    _detectedPath = trimmed;
                    FileEditButton.Visibility = Visibility.Visible;
                    FileOpenButton.Visibility = Visibility.Visible;
                }
                else if (Directory.Exists(trimmed))
                {
                    _detectedPath = trimmed;
                    FolderOpenButton.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                // 유효하지 않은 경로 문자열이면 무시
            }

            // URL 감지
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                _detectedUrl = trimmed;
                UrlOpenButton.Visibility = Visibility.Visible;
            }
        }

        // HTML 포맷 데이터가 있으면 서식 보기 버튼 표시
        if (!string.IsNullOrEmpty(_htmlContent))
        {
            HtmlViewButton.Visibility = Visibility.Visible;
        }

        if (isCompact) ApplyCompactMode();

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(durationSeconds)
        };
        _autoCloseTimer.Tick += (s, e) => FadeOutAndClose();
        _autoCloseTimer.Start();
    }

    public ToastWindow(BitmapSource image, ToastPosition position, int durationSeconds, bool isCompact = false)
    {
        InitializeComponent();
        _targetOpacity = (App.SettingsService?.Settings.ToastOpacityPercent ?? 100) / 100.0;
        _imageContent = image;
        _position = position;

        // 버튼 텍스트 다국어 적용
        SaveButton.Content = Loc.BtnSave;
        EditButton.Content = Loc.BtnEdit;

        MessageText.Text = Loc.ImageCopied;
        PreviewText.Text = $"{image.PixelWidth} x {image.PixelHeight} px";
        
        // 이미지 미리보기 표시
        ImagePreview.Source = image;
        ImagePreviewBorder.Visibility = Visibility.Visible;

        if (isCompact) ApplyCompactMode();

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(durationSeconds)
        };
        _autoCloseTimer.Tick += (s, e) => FadeOutAndClose();
        _autoCloseTimer.Start();
    }

    /// <summary>
    /// 파일 복사용 생성자
    /// </summary>
    public ToastWindow(string filePath, bool isFile, ToastPosition position, int durationSeconds, bool isCompact = false)
    {
        InitializeComponent();
        _targetOpacity = (App.SettingsService?.Settings.ToastOpacityPercent ?? 100) / 100.0;
        _filePath = filePath;
        _position = position;

        // 버튼 텍스트 다국어 적용
        EditButton.Content = Loc.BtnEdit;

        var fileName = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);
        var sizeText = Loc.FormatSize(fileInfo.Length);

        MessageText.Text = Loc.FileCopied;
        PreviewText.Text = $"{fileName} ({sizeText})";

        // 이미지 파일인 경우 미리보기 표시
        var extension = Path.GetExtension(filePath).ToLower();
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".ico" };
        if (imageExtensions.Contains(extension))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 60; // 작은 크기로 디코딩
                bitmap.EndInit();
                bitmap.Freeze();
                
                ImagePreview.Source = bitmap;
                ImagePreviewBorder.Visibility = Visibility.Visible;
            }
            catch { /* 이미지 로드 실패 시 무시 */ }
        }

        // 파일 복사 시 저장 버튼 비활성화
        SaveButton.Visibility = Visibility.Collapsed;

        if (isCompact) ApplyCompactMode();

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(durationSeconds)
        };
        _autoCloseTimer.Tick += (s, e) => FadeOutAndClose();
        _autoCloseTimer.Start();
    }

    /// <summary>
    /// 설정 미리보기용 생성자
    /// </summary>
    internal ToastWindow(ToastPosition position, bool isCompact, int opacityPercent)
    {
        InitializeComponent();
        _targetOpacity = opacityPercent / 100.0;
        _position = position;
        _textContent = Loc.ToastPreviewText;

        SaveButton.Content = Loc.BtnSave;
        EditButton.Content = Loc.BtnEdit;

        MessageText.Text = Loc.ToastPreviewText;
        PreviewText.Text = "Hello, AutoPad!";

        if (isCompact) ApplyCompactMode();

        // 미리보기용 - 자동 닫기 없음, 버튼 비활성화
        _autoCloseTimer = new DispatcherTimer();
        SaveButton.IsEnabled = false;
        EditButton.IsEnabled = false;
        HtmlViewButton.IsEnabled = false;
        FileEditButton.IsEnabled = false;
        FileOpenButton.IsEnabled = false;
        FolderOpenButton.IsEnabled = false;
        UrlOpenButton.IsEnabled = false;
        CloseBtn.IsEnabled = false;
    }

    /// <summary>
    /// 미리보기 투명도 실시간 업데이트
    /// </summary>
    internal void UpdatePreviewOpacity(double opacity)
    {
        _targetOpacity = opacity;
        ToastBorder.BeginAnimation(UIElement.OpacityProperty, null);
        ToastBorder.Opacity = opacity;
    }

    private void ApplyCompactMode()
    {
        MessageText.Visibility = Visibility.Collapsed;
        PreviewText.Visibility = Visibility.Collapsed;
        ImagePreviewBorder.Visibility = Visibility.Collapsed;
        CloseBtn.Visibility = Visibility.Collapsed;
        SizeToContent = SizeToContent.WidthAndHeight;

        // 버튼 패딩 축소
        foreach (var btn in new[] { SaveButton, EditButton, HtmlViewButton, FileEditButton, FileOpenButton, FolderOpenButton, UrlOpenButton })
        {
            btn.Padding = new Thickness(10, 5, 10, 5);
            btn.FontSize = 12;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        // Fade in to target opacity
        var fadeIn = new DoubleAnimation(0, _targetOpacity, TimeSpan.FromMilliseconds(200));
        ToastBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    private void FadeOutAndClose()
    {
        _autoCloseTimer.Stop();
        var fadeOut = new DoubleAnimation(ToastBorder.Opacity, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (s, e) => Close();
        ToastBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        var w = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 300 : Width);
        var h = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 80 : Height);

        Left = _position switch
        {
            ToastPosition.BottomLeft or ToastPosition.TopLeft => workArea.Left + 20,
            ToastPosition.BottomRight or ToastPosition.TopRight => workArea.Right - w - 20,
            _ => workArea.Left + (workArea.Width - w) / 2
        };

        Top = _position switch
        {
            ToastPosition.Top or ToastPosition.TopLeft or ToastPosition.TopRight => workArea.Top + 20,
            _ => workArea.Bottom - h - 20
        };
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        Close();

        var isDarkMode = App.SettingsService?.Settings.IsDarkMode ?? true;

        if (_textContent != null)
        {
            var editWindow = new EditWindow(_textContent, isDarkMode);
            editWindow.Show();
        }
        else if (_imageContent != null)
        {
            var editWindow = new EditWindow(_imageContent, isDarkMode);
            editWindow.Show();
        }
        else if (_filePath != null)
        {
            var editWindow = new EditWindow(_filePath, isFilePath: true, isDarkMode);
            editWindow.Show();
        }

        EditRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FileEditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detectedPath == null) return;
        
        _autoCloseTimer.Stop();
        Close();

        var isDarkMode = App.SettingsService?.Settings.IsDarkMode ?? true;
        var editWindow = new EditWindow(_detectedPath, isFilePath: true, isDarkMode);
        editWindow.Show();
    }

    private void HtmlViewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_htmlContent == null) return;

        _autoCloseTimer.Stop();
        Close();

        var isDarkMode = App.SettingsService?.Settings.IsDarkMode ?? true;
        var viewer = new HtmlViewerWindow(_htmlContent, isDarkMode);
        viewer.Show();
    }

    private void FileOpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detectedPath == null) return;
        
        _autoCloseTimer.Stop();
        
        try
        {
            Process.Start(new ProcessStartInfo(_detectedPath) { UseShellExecute = true });
        }
        catch { }
        
        Close();
    }

    private void FolderOpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detectedPath == null) return;
        
        _autoCloseTimer.Stop();
        
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_detectedPath}\""));
        }
        catch { }
        
        Close();
    }

    private void UrlOpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detectedUrl == null) return;
        
        _autoCloseTimer.Stop();
        
        try
        {
            Process.Start(new ProcessStartInfo(_detectedUrl) { UseShellExecute = true });
        }
        catch { }
        
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();

        if (_textContent != null)
        {
            SaveTextFile(_textContent);
        }
        else if (_imageContent != null)
        {
            SaveImageFile(_imageContent);
        }

        Close();
    }

    private void SaveTextFile(string text)
    {
        var dialog = new WpfSaveFileDialog
        {
            Filter = Loc.TextFileFilter,
            DefaultExt = ".txt",
            FileName = $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, text);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(Loc.SaveFailed(ex.Message), Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveImageFile(BitmapSource image)
    {
        var dialog = new WpfSaveFileDialog
        {
            Filter = Loc.ImageFileFilter,
            DefaultExt = ".png",
            FileName = $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                BitmapEncoder encoder = Path.GetExtension(dialog.FileName).ToLower() switch
                {
                    ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
                    ".bmp" => new BmpBitmapEncoder(),
                    _ => new PngBitmapEncoder()
                };

                encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(dialog.FileName);
                encoder.Save(stream);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(Loc.SaveFailed(ex.Message), Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        FadeOutAndClose();
    }

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _autoCloseTimer.Stop();
        if (_targetOpacity < 1.0)
        {
            var fadeIn = new DoubleAnimation(ToastBorder.Opacity, 1.0, TimeSpan.FromMilliseconds(150));
            ToastBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _autoCloseTimer.Start();
        if (_targetOpacity < 1.0)
        {
            var fadeOut = new DoubleAnimation(ToastBorder.Opacity, _targetOpacity, TimeSpan.FromMilliseconds(150));
            ToastBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}
