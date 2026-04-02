using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoPad.Services;
using WpfColor = System.Windows.Media.Color;

namespace AutoPad.Windows;

public partial class StickyWindow : Window
{
    private readonly string? _text;
    private readonly BitmapSource? _image;
    private readonly bool _isImageMode;
    private bool _isFitToWindow = true;
    private bool _isTopMost = true;

    private System.Windows.Controls.TextBlock? _pinIconText;
    private System.Windows.Controls.TextBlock? _fitIconText;

    /// <summary>
    /// 텍스트 스티커
    /// </summary>
    public StickyWindow(string text)
    {
        InitializeComponent();
        _text = text;
        _isImageMode = false;

        Icon = IconHelper.CreateAppIconImageSource(32);
        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, true);

        ContentTextBox.Text = text;
        ContentTextBox.Visibility = Visibility.Visible;

        var runeCount = text.EnumerateRunes().Count();
        TitleText.Text = Loc.StickyTitleText(runeCount);

        Topmost = true;

        EditButton.ToolTip = Loc.StickyEdit;
        TopMostButton.ToolTip = Loc.StickyTopMost;

        Loaded += (s, e) => UpdateTopMostVisual();
    }

    /// <summary>
    /// 이미지 스티커
    /// </summary>
    public StickyWindow(BitmapSource image)
    {
        InitializeComponent();
        _image = image;
        _isImageMode = true;

        Icon = IconHelper.CreateAppIconImageSource(32);
        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, true);

        ContentImage.Source = image;
        ImageContainer.Visibility = Visibility.Visible;

        TitleText.Text = Loc.StickyTitleImage(image.PixelWidth, image.PixelHeight);

        FitToggleButton.Visibility = Visibility.Visible;
        FitToggleButton.ToolTip = Loc.ToolFitToggle;

        Topmost = true;

        EditButton.ToolTip = Loc.StickyEdit;
        TopMostButton.ToolTip = Loc.StickyTopMost;

        Loaded += (s, e) =>
        {
            UpdateTopMostVisual();
            UpdateFitVisual();
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 더블클릭 → 편집 모드
            OpenEditWindow();
            return;
        }
        DragMove();
    }

    private void TopMostButton_Click(object sender, RoutedEventArgs e)
    {
        _isTopMost = !_isTopMost;
        Topmost = _isTopMost;
        UpdateTopMostVisual();
    }

    private void UpdateTopMostVisual()
    {
        if (_pinIconText == null)
        {
            TopMostButton.ApplyTemplate();
            _pinIconText = (System.Windows.Controls.TextBlock?)TopMostButton.Template.FindName("PinIconText", TopMostButton);
        }
        if (_pinIconText != null)
        {
            _pinIconText.Foreground = _isTopMost
                ? new SolidColorBrush(WpfColor.FromRgb(0x00, 0x78, 0xD4))
                : new SolidColorBrush(WpfColor.FromRgb(0xAA, 0xAA, 0xAA));
        }
    }

    private void FitToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isFitToWindow = !_isFitToWindow;
        UpdateFitVisual();
    }

    private void UpdateFitVisual()
    {
        if (_image == null) return;

        if (_fitIconText == null)
        {
            FitToggleButton.ApplyTemplate();
            _fitIconText = (System.Windows.Controls.TextBlock?)FitToggleButton.Template.FindName("FitIconText", FitToggleButton);
        }

        if (_isFitToWindow)
        {
            ContentImage.Stretch = Stretch.Uniform;
            ContentImage.StretchDirection = System.Windows.Controls.StretchDirection.DownOnly;
            if (_fitIconText != null) _fitIconText.Text = "\uE71E";
            FitToggleButton.ToolTip = Loc.FitOriginal;
        }
        else
        {
            ContentImage.Stretch = Stretch.None;
            ContentImage.StretchDirection = System.Windows.Controls.StretchDirection.Both;
            if (_fitIconText != null) _fitIconText.Text = "\uE9A6";
            FitToggleButton.ToolTip = Loc.FitToWindow;
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        OpenEditWindow();
    }

    private void OpenEditWindow()
    {
        if (_isImageMode && _image != null)
        {
            var editWindow = new EditWindow(_image);
            editWindow.Show();
        }
        else if (_text != null)
        {
            var editWindow = new EditWindow(_text);
            editWindow.Show();
        }
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
