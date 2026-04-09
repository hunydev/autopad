using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoPad.Services;
using WpfColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;

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
    private Border? _colorSwatch;

    private static readonly string[] TitleBarColors =
    [
        "#252526", "#2B4C7E", "#1E6F50", "#8B3A3A", "#6B3FA0",
        "#8B6914", "#2E86AB", "#C0392B", "#1ABC9C", "#7D5A50"
    ];

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
        ColorButton.ToolTip = Loc.StickyColor;

        Loaded += (s, e) =>
        {
            UpdateTopMostVisual();
            BuildColorPalette();
        };
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
        ColorButton.ToolTip = Loc.StickyColor;

        Loaded += (s, e) =>
        {
            UpdateTopMostVisual();
            UpdateFitVisual();
            BuildColorPalette();
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
            ContentImage.StretchDirection = System.Windows.Controls.StretchDirection.Both;
            ContentImage.Width = double.NaN;
            ContentImage.Height = double.NaN;
            ContentImage.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            ContentImage.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            ImageScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            ImageScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            if (_fitIconText != null) _fitIconText.Text = "\uE71E";
            FitToggleButton.ToolTip = Loc.FitOriginal;
        }
        else
        {
            ContentImage.Stretch = Stretch.None;
            ContentImage.StretchDirection = System.Windows.Controls.StretchDirection.Both;
            ContentImage.Width = _image.PixelWidth;
            ContentImage.Height = _image.PixelHeight;
            ContentImage.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            ContentImage.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            ImageScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            ImageScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            if (_fitIconText != null) _fitIconText.Text = "\uE9A6";
            FitToggleButton.ToolTip = Loc.FitToWindow;
        }
    }

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
        {
            return;
        }

        var targetOffset = ImageScrollViewer.HorizontalOffset - e.Delta;
        ImageScrollViewer.ScrollToHorizontalOffset(targetOffset);
        e.Handled = true;
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

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        ColorPopup.IsOpen = !ColorPopup.IsOpen;
    }

    private void BuildColorPalette()
    {
        ColorPalettePanel.Children.Clear();
        foreach (var hex in TitleBarColors)
        {
            var color = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var btn = new WpfButton
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = hex,
                Background = new SolidColorBrush(color)
            };
            btn.Template = CreateColorSwatchTemplate();
            btn.Click += ColorPaletteItem_Click;
            ColorPalettePanel.Children.Add(btn);
        }
    }

    private static ControlTemplate CreateColorSwatchTemplate()
    {
        var template = new ControlTemplate(typeof(WpfButton));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.BorderBrushProperty, System.Windows.Media.Brushes.Transparent);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        border.Name = "Bd";
        template.VisualTree = border;

        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BorderBrushProperty, System.Windows.Media.Brushes.White, "Bd"));
        template.Triggers.Add(trigger);

        return template;
    }

    private void ColorPaletteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn || btn.Tag is not string hex) return;
        ColorPopup.IsOpen = false;

        var color = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        TitleBar.Background = new SolidColorBrush(color);
        UpdateColorSwatch(color);
    }

    private void UpdateColorSwatch(WpfColor color)
    {
        if (_colorSwatch == null)
        {
            ColorButton.ApplyTemplate();
            _colorSwatch = (Border?)ColorButton.Template.FindName("ColorSwatch", ColorButton);
        }
        if (_colorSwatch != null)
        {
            _colorSwatch.Background = new SolidColorBrush(color);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
