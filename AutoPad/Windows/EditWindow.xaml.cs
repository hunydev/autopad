using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AutoPad.Services;
using WpfClipboard = System.Windows.Clipboard;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace AutoPad.Windows;

public partial class EditWindow : Window
{
    private readonly bool _isImageMode;
    private readonly string? _sourceFilePath;
    private readonly BitmapSource? _originalImage;
    private WpfColor _currentColor = Colors.Black;
    private WpfColor _fillColor = Colors.Black;
    private readonly List<WpfButton> _colorButtons = new();
    private readonly StrokeCollection _undoStack = new();
    private Encoding _currentEncoding = Encoding.UTF8;
    private bool _isFitToWindow = false;

    // 영역 선택 관련
    private bool _isSelecting = false;
    private bool _isSelectMode = false;
    private WpfPoint _selectionStart;
    private WpfRect _selectionRect;
    private bool _hasSelection = false;

    // Undo 지원: 이미지 상태 스택
    private readonly Stack<BitmapSource> _imageUndoStack = new();

    public static bool SuppressClipboardMonitor { get; private set; }

    private void ApplyLocalization()
    {
        SubtitleText.Text = Loc.EditSubtitle;
        EncodingLabel.Text = Loc.LabelEncoding;
        PenToolButton.ToolTip = Loc.ToolPen;
        EraserToolButton.ToolTip = Loc.ToolEraser;
        SelectToolButton.ToolTip = Loc.ToolSelect;
        ThicknessLabel.Text = Loc.LabelThickness;
        EraseByStrokeButton.ToolTip = Loc.ToolEraseStrokeTip;
        EraseByStrokeText.Text = Loc.ToolEraseStroke;
        EraseByPointButton.ToolTip = Loc.ToolErasePointTip;
        EraseByPointText.Text = Loc.ToolErasePoint;
        EraserSizeLabel.Text = Loc.LabelSize;
        MosaicSelectionButton.ToolTip = Loc.ToolMosaicTip;
        MosaicText.Text = Loc.ToolMosaic;
        FillSelectionButton.ToolTip = Loc.ToolFillTip;
        FillText.Text = Loc.ToolFill;
        EraseSelectionButton.ToolTip = Loc.ToolEraseTip;
        EraseText.Text = Loc.ToolErase;
        UndoButton.ToolTip = Loc.ToolUndo;
        ClearButton.ToolTip = Loc.ToolClearAll;
        FitToggleButton.ToolTip = Loc.ToolFitToggle;
        FitToggleText.Text = Loc.FitToWindow;
        SaveButton.Content = Loc.BtnSaveFile;
        CopyAllButton.Content = Loc.BtnCopyClose;
    }

    public EditWindow(string text, bool isDarkMode = true)
    {
        InitializeComponent();
        ApplyLocalization();
        _isImageMode = false;
        
        Icon = IconHelper.CreateAppIconImageSource(32);
        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, isDarkMode);
        
        ContentTextBox.Text = text;
        ContentTextBox.Focus();
        ContentTextBox.SelectAll();
        
        var byteSize = Encoding.UTF8.GetByteCount(text);
        var runeCount = text.EnumerateRunes().Count();
        Title = Loc.EditTitle(runeCount, FormatSize(byteSize));
        
        CopySelectedButton.Content = Loc.BtnCopySelection;
        SaveButton.Visibility = Visibility.Visible;
    }

    public EditWindow(BitmapSource image, bool isDarkMode = true)
    {
        InitializeComponent();
        ApplyLocalization();
        _isImageMode = true;
        _originalImage = image;
        
        Icon = IconHelper.CreateAppIconImageSource(32);
        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, isDarkMode);
        
        ContentTextBox.Visibility = Visibility.Collapsed;
        ImageContainer.Visibility = Visibility.Visible;
        ImageToolbar.Visibility = Visibility.Visible;
        ContentImage.Source = image;
        
        Title = Loc.EditTitleImage(image.PixelWidth, image.PixelHeight);
        
        SetImageOriginalSize(image);
        SetupDrawingCanvas();
        InitColorButtons();
        
        CopySelectedButton.Content = Loc.BtnCopySelection;
        CopySelectedButton.Visibility = Visibility.Collapsed;
        CopyAllButton.Content = Loc.BtnCopyClose;
        SaveButton.Visibility = Visibility.Visible;
        
        KeyDown += EditWindow_KeyDown;
        SetupSelectionEvents();
        SizeChanged += OnWindowSizeChanged;
    }

    public EditWindow(string filePath, bool isFilePath, bool isDarkMode = true)
    {
        InitializeComponent();
        ApplyLocalization();
        _sourceFilePath = filePath;
        
        Icon = IconHelper.CreateAppIconImageSource(32);
        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, isDarkMode);
        
        Title = $"AutoPad - {Path.GetFileName(filePath)}";
        CopySelectedButton.Content = Loc.BtnCopySelection;
        CopyAllButton.Content = Loc.BtnCopyClose;
        
        var extension = Path.GetExtension(filePath).ToLower();
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".ico" };
        
        if (imageExtensions.Contains(extension))
        {
            _isImageMode = true;
            
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                
                _originalImage = bitmap;
                
                ContentTextBox.Visibility = Visibility.Collapsed;
                ImageContainer.Visibility = Visibility.Visible;
                ImageToolbar.Visibility = Visibility.Visible;
                ContentImage.Source = bitmap;
                
                SetImageOriginalSize(bitmap);
                SetupDrawingCanvas();
                InitColorButtons();
                
                CopySelectedButton.Content = Loc.BtnCopySelection;
                CopySelectedButton.Visibility = Visibility.Collapsed;
                SetupSelectionEvents();
                SizeChanged += OnWindowSizeChanged;
            }
            catch (Exception ex)
            {
                ShowStatus(Loc.ImageLoadFailed(ex.Message));
                _isImageMode = false;
                LoadFileAsText(filePath);
            }
        }
        else
        {
            _isImageMode = false;
            LoadFileAsText(filePath);
            EncodingToolbar.Visibility = Visibility.Visible;
        }
        
        SaveButton.Visibility = Visibility.Visible;
        KeyDown += EditWindow_KeyDown;
    }

    private void InitColorButtons()
    {
        _colorButtons.AddRange(new[] { ColorBlack, ColorRed, ColorOrange, ColorYellow, ColorGreen, ColorBlue, ColorPurple, ColorWhite });
    }

    private void SetImageOriginalSize(BitmapSource image)
    {
        double width = image.PixelWidth;
        double height = image.PixelHeight;
        
        CanvasContainer.Width = width;
        CanvasContainer.Height = height;
        DrawingCanvas.Width = width;
        DrawingCanvas.Height = height;
        ContentImage.Width = width;
        ContentImage.Height = height;
    }

    private void SetupSelectionEvents()
    {
        CanvasContainer.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
        CanvasContainer.MouseMove += Canvas_MouseMove;
        CanvasContainer.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelectMode) return;
        
        _isSelecting = true;
        _selectionStart = e.GetPosition(CanvasContainer);
        _hasSelection = false;
        
        Canvas.SetLeft(SelectionRectangle, _selectionStart.X);
        Canvas.SetTop(SelectionRectangle, _selectionStart.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        SelectionRectangle.Visibility = Visibility.Visible;
        
        CanvasContainer.CaptureMouse();
        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isSelecting) return;
        
        var currentPos = e.GetPosition(CanvasContainer);
        
        var x = Math.Min(_selectionStart.X, currentPos.X);
        var y = Math.Min(_selectionStart.Y, currentPos.Y);
        var w = Math.Abs(currentPos.X - _selectionStart.X);
        var h = Math.Abs(currentPos.Y - _selectionStart.Y);
        
        // Clamp to canvas bounds
        if (_originalImage != null)
        {
            x = Math.Max(0, x);
            y = Math.Max(0, y);
            w = Math.Min(w, _originalImage.PixelWidth - x);
            h = Math.Min(h, _originalImage.PixelHeight - y);
        }
        
        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = Math.Max(0, w);
        SelectionRectangle.Height = Math.Max(0, h);
        
        _selectionRect = new WpfRect(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        
        _isSelecting = false;
        CanvasContainer.ReleaseMouseCapture();
        
        if (_selectionRect.Width > 5 && _selectionRect.Height > 5)
        {
            _hasSelection = true;
            CopySelectedButton.Visibility = Visibility.Visible;
        }
        else
        {
            _hasSelection = false;
            SelectionRectangle.Visibility = Visibility.Collapsed;
            CopySelectedButton.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadFileAsText(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            ContentTextBox.Text = _currentEncoding.GetString(bytes);
            ContentTextBox.Focus();
        }
        catch (Exception ex)
        {
            ContentTextBox.Text = Loc.FileReadFailed(ex.Message);
        }
    }

    private bool _isChangingEncoding = false;

    private void EncodingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _sourceFilePath == null) return;
        if (_isChangingEncoding) return;
        _isChangingEncoding = true;
        
        try
        {
            if (EncodingComboBox.SelectedItem is WpfComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                var encoding = tag == "EUCKR" ? Encoding.GetEncoding(949) : Encoding.UTF8;
                _currentEncoding = encoding;
                
                var filePath = _sourceFilePath;
                if (!File.Exists(filePath))
                {
                    ContentTextBox.Text = Loc.FileNotFound;
                    return;
                }
                
                var bytes = File.ReadAllBytes(filePath);
                ContentTextBox.Text = encoding.GetString(bytes);
            }
        }
        catch (Exception ex)
        {
            ContentTextBox.Text = Loc.FileReadFailed(ex.Message);
        }
        finally
        {
            _isChangingEncoding = false;
        }
    }

    private void SetupDrawingCanvas()
    {
        var attributes = new DrawingAttributes
        {
            Color = _currentColor,
            Width = BrushSizeSlider.Value,
            Height = BrushSizeSlider.Value,
            FitToCurve = true,
            StylusTip = StylusTip.Ellipse
        };
        DrawingCanvas.DefaultDrawingAttributes = attributes;
    }

    private void EditWindow_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_hasSelection)
            {
                ClearSelection();
                e.Handled = true;
                return;
            }
            Close();
            e.Handled = true;
            return;
        }
        
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            UndoLastStroke();
            e.Handled = true;
        }
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        _isSelecting = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;
        if (_isSelectMode)
        {
            CopySelectedButton.Visibility = Visibility.Collapsed;
        }
    }

    private void PenToolButton_Click(object sender, RoutedEventArgs e)
    {
        _isSelectMode = false;
        DrawingCanvas.EditingMode = InkCanvasEditingMode.Ink;
        DrawingCanvas.IsHitTestVisible = true;
        PenToolButton.Tag = "Selected";
        EraserToolButton.Tag = null;
        SelectToolButton.Tag = null;
        
        PenOptionsPanel.Visibility = Visibility.Visible;
        EraserOptionsPanel.Visibility = Visibility.Collapsed;
        SelectOptionsPanel.Visibility = Visibility.Collapsed;
        ClearSelection();
        CopySelectedButton.Visibility = Visibility.Collapsed;
    }

    private void EraserToolButton_Click(object sender, RoutedEventArgs e)
    {
        _isSelectMode = false;
        DrawingCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        DrawingCanvas.IsHitTestVisible = true;
        EraserToolButton.Tag = "Selected";
        PenToolButton.Tag = null;
        SelectToolButton.Tag = null;
        
        // 지우개 서브모드 초기화
        EraseByStrokeButton.Tag = "Selected";
        EraseByPointButton.Tag = null;
        
        PenOptionsPanel.Visibility = Visibility.Collapsed;
        EraserOptionsPanel.Visibility = Visibility.Visible;
        SelectOptionsPanel.Visibility = Visibility.Collapsed;
        ClearSelection();
        CopySelectedButton.Visibility = Visibility.Collapsed;
    }

    private void EraseByStrokeButton_Click(object sender, RoutedEventArgs e)
    {
        DrawingCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        EraseByStrokeButton.Tag = "Selected";
        EraseByPointButton.Tag = null;
    }

    private void EraseByPointButton_Click(object sender, RoutedEventArgs e)
    {
        DrawingCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
        DrawingCanvas.EraserShape = new RectangleStylusShape(EraserSizeSlider.Value, EraserSizeSlider.Value);
        EraseByPointButton.Tag = "Selected";
        EraseByStrokeButton.Tag = null;
    }

    private void EraserSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DrawingCanvas != null && DrawingCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
        {
            DrawingCanvas.EraserShape = new RectangleStylusShape(e.NewValue, e.NewValue);
        }
    }

    private void SelectToolButton_Click(object sender, RoutedEventArgs e)
    {
        _isSelectMode = true;
        DrawingCanvas.EditingMode = InkCanvasEditingMode.None;
        DrawingCanvas.IsHitTestVisible = false;
        SelectToolButton.Tag = "Selected";
        PenToolButton.Tag = null;
        EraserToolButton.Tag = null;
        
        PenOptionsPanel.Visibility = Visibility.Collapsed;
        EraserOptionsPanel.Visibility = Visibility.Collapsed;
        SelectOptionsPanel.Visibility = Visibility.Visible;
        ClearSelection();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isFitToWindow && _originalImage != null)
        {
            ApplyFitToWindow();
        }
    }

    private void FitToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_originalImage == null) return;
        
        _isFitToWindow = !_isFitToWindow;
        
        if (_isFitToWindow)
        {
            // 먼저 스트로크를 원본 좌표로 보존
            ApplyFitToWindow();
            
            ImageScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            ImageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            
            FitToggleText.Text = Loc.FitOriginal;
            FitToggleIcon.Text = "\uE71E";
        }
        else
        {
            // 원본 크기 모드 복원
            RestoreOriginalSize();
            
            ImageScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            ImageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            
            FitToggleText.Text = Loc.FitToWindow;
            FitToggleIcon.Text = "\uE9A6";
        }
    }

    private double _currentScale = 1.0;

    private void ApplyFitToWindow()
    {
        if (_originalImage == null) return;
        
        var scrollViewerHeight = ImageScrollViewer.ActualHeight;
        if (scrollViewerHeight <= 0)
            scrollViewerHeight = ImageContainer.ActualHeight - 2;
        if (scrollViewerHeight <= 0) return;
        
        var imgWidth = (double)_originalImage.PixelWidth;
        var imgHeight = (double)_originalImage.PixelHeight;
        
        var newScale = scrollViewerHeight / imgHeight;
        // 이미지가 원본보다 큰 경우에도 맞추기
        var fitWidth = imgWidth * newScale;
        var fitHeight = imgHeight * newScale;
        
        // 스트로크 스케일 변환: 현재 스케일에서 새 스케일로
        if (Math.Abs(_currentScale - newScale) > 0.001 && DrawingCanvas.Strokes.Count > 0)
        {
            var ratio = newScale / _currentScale;
            var matrix = new Matrix();
            matrix.Scale(ratio, ratio);
            foreach (var stroke in DrawingCanvas.Strokes)
            {
                stroke.Transform(matrix, false);
            }
        }
        
        _currentScale = newScale;
        
        CanvasContainer.Width = fitWidth;
        CanvasContainer.Height = fitHeight;
        ContentImage.Width = fitWidth;
        ContentImage.Height = fitHeight;
        ContentImage.Stretch = Stretch.Uniform;
        DrawingCanvas.Width = fitWidth;
        DrawingCanvas.Height = fitHeight;
    }

    private void RestoreOriginalSize()
    {
        if (_originalImage == null) return;
        
        var imgWidth = (double)_originalImage.PixelWidth;
        var imgHeight = (double)_originalImage.PixelHeight;
        
        // 스트로크 역변환
        if (Math.Abs(_currentScale - 1.0) > 0.001 && DrawingCanvas.Strokes.Count > 0)
        {
            var ratio = 1.0 / _currentScale;
            var matrix = new Matrix();
            matrix.Scale(ratio, ratio);
            foreach (var stroke in DrawingCanvas.Strokes)
            {
                stroke.Transform(matrix, false);
            }
        }
        
        _currentScale = 1.0;
        
        ContentImage.Stretch = Stretch.None;
        SetImageOriginalSize(_originalImage);
    }

    private void FillSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasSelection || _originalImage == null) return;
        
        // 색상 선택 다이얼로그
        var colorDialog = new System.Windows.Forms.ColorDialog();
        colorDialog.Color = System.Drawing.Color.FromArgb(_fillColor.R, _fillColor.G, _fillColor.B);
        colorDialog.FullOpen = true;
        
        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _fillColor = WpfColor.FromRgb(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
            ApplyFillToSelection();
        }
    }

    private void MosaicSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasSelection || _originalImage == null) return;
        ApplyMosaicToSelection();
    }

    private void EraseSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasSelection || _originalImage == null) return;
        ApplyEraseToSelection();
    }

    private void SaveImageStateForUndo()
    {
        // 현재 소스를 undo 스택에 저장
        var currentSource = ContentImage.Source as BitmapSource;
        if (currentSource != null)
        {
            _imageUndoStack.Push(currentSource);
        }
    }

    private void ApplyFillToSelection()
    {
        if (_originalImage == null) return;
        
        SaveImageStateForUndo();
        
        var currentImage = RenderImageWithDrawing();
        DrawingCanvas.Strokes.Clear();
        
        var width = currentImage.PixelWidth;
        var height = currentImage.PixelHeight;
        
        // Calculate actual selection rect in image coordinates
        var rect = GetImageSpaceSelectionRect();
        
        var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            context.DrawImage(currentImage, new WpfRect(0, 0, width, height));
            
            var brush = new SolidColorBrush(WpfColor.FromRgb(_fillColor.R, _fillColor.G, _fillColor.B));
            context.DrawRectangle(brush, null, rect);
        }
        renderTarget.Render(drawingVisual);
        
        var frozen = BitmapFrame.Create(renderTarget);
        frozen.Freeze();
        
        ContentImage.Source = frozen;
        ClearSelection();
        ShowStatus(Loc.StatusFilled);
    }

    private void ApplyMosaicToSelection()
    {
        if (_originalImage == null) return;
        
        SaveImageStateForUndo();
        
        var currentImage = RenderImageWithDrawing();
        DrawingCanvas.Strokes.Clear();
        
        var width = currentImage.PixelWidth;
        var height = currentImage.PixelHeight;
        
        var rect = GetImageSpaceSelectionRect();
        
        // Get pixels
        var stride = width * 4;
        var pixels = new byte[stride * height];
        currentImage.CopyPixels(pixels, stride, 0);
        
        int blockSize = Math.Max(8, (int)(Math.Min(rect.Width, rect.Height) / 10));
        
        int startX = Math.Max(0, (int)rect.X);
        int startY = Math.Max(0, (int)rect.Y);
        int endX = Math.Min(width, (int)(rect.X + rect.Width));
        int endY = Math.Min(height, (int)(rect.Y + rect.Height));
        
        for (int by = startY; by < endY; by += blockSize)
        {
            for (int bx = startX; bx < endX; bx += blockSize)
            {
                int bw = Math.Min(blockSize, endX - bx);
                int bh = Math.Min(blockSize, endY - by);
                
                // Average color in block
                long totalR = 0, totalG = 0, totalB = 0, totalA = 0;
                int count = 0;
                
                for (int py = by; py < by + bh; py++)
                {
                    for (int px = bx; px < bx + bw; px++)
                    {
                        int idx = py * stride + px * 4;
                        totalB += pixels[idx];
                        totalG += pixels[idx + 1];
                        totalR += pixels[idx + 2];
                        totalA += pixels[idx + 3];
                        count++;
                    }
                }
                
                if (count == 0) continue;
                
                byte avgB = (byte)(totalB / count);
                byte avgG = (byte)(totalG / count);
                byte avgR = (byte)(totalR / count);
                byte avgA = (byte)(totalA / count);
                
                // Fill block with average
                for (int py = by; py < by + bh; py++)
                {
                    for (int px = bx; px < bx + bw; px++)
                    {
                        int idx = py * stride + px * 4;
                        pixels[idx] = avgB;
                        pixels[idx + 1] = avgG;
                        pixels[idx + 2] = avgR;
                        pixels[idx + 3] = avgA;
                    }
                }
            }
        }
        
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        
        ContentImage.Source = result;
        ClearSelection();
        ShowStatus(Loc.StatusMosaic);
    }

    private void ApplyEraseToSelection()
    {
        if (_originalImage == null) return;
        
        SaveImageStateForUndo();
        
        var currentImage = RenderImageWithDrawing();
        DrawingCanvas.Strokes.Clear();
        
        var width = currentImage.PixelWidth;
        var height = currentImage.PixelHeight;
        
        var rect = GetImageSpaceSelectionRect();
        
        var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            context.DrawImage(currentImage, new WpfRect(0, 0, width, height));
            // Draw transparent rectangle (erase)
            context.PushOpacityMask(null);
            context.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, rect);
            context.Pop();
        }
        renderTarget.Render(drawingVisual);
        
        // Manually erase pixels in the selected region
        var stride = width * 4;
        var pixels = new byte[stride * height];
        renderTarget.CopyPixels(pixels, stride, 0);
        
        int startX = Math.Max(0, (int)rect.X);
        int startY = Math.Max(0, (int)rect.Y);
        int endX = Math.Min(width, (int)(rect.X + rect.Width));
        int endY = Math.Min(height, (int)(rect.Y + rect.Height));
        
        for (int py = startY; py < endY; py++)
        {
            for (int px = startX; px < endX; px++)
            {
                int idx = py * stride + px * 4;
                pixels[idx] = 0;     // B
                pixels[idx + 1] = 0; // G
                pixels[idx + 2] = 0; // R
                pixels[idx + 3] = 0; // A (transparent)
            }
        }
        
        var erased = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        erased.Freeze();
        
        ContentImage.Source = erased;
        ClearSelection();
        ShowStatus(Loc.StatusErased);
    }

    private WpfRect GetImageSpaceSelectionRect()
    {
        if (_originalImage == null) return _selectionRect;
        
        if (_isFitToWindow)
        {
            var scaleX = _originalImage.PixelWidth / CanvasContainer.Width;
            var scaleY = _originalImage.PixelHeight / CanvasContainer.Height;
            return new WpfRect(
                _selectionRect.X * scaleX,
                _selectionRect.Y * scaleY,
                _selectionRect.Width * scaleX,
                _selectionRect.Height * scaleY);
        }
        return _selectionRect;
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton btn && btn.Background is SolidColorBrush brush)
        {
            _currentColor = brush.Color;
            
            foreach (var colorBtn in _colorButtons)
            {
                colorBtn.Tag = null;
            }
            btn.Tag = "Selected";
            
            DrawingCanvas.DefaultDrawingAttributes.Color = _currentColor;
            
            DrawingCanvas.EditingMode = InkCanvasEditingMode.Ink;
            PenToolButton.Tag = "Selected";
            EraserToolButton.Tag = null;
            SelectToolButton.Tag = null;
            _isSelectMode = false;
            PenOptionsPanel.Visibility = Visibility.Visible;
            SelectOptionsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DrawingCanvas?.DefaultDrawingAttributes != null)
        {
            DrawingCanvas.DefaultDrawingAttributes.Width = e.NewValue;
            DrawingCanvas.DefaultDrawingAttributes.Height = e.NewValue;
        }
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        UndoLastStroke();
    }

    private void UndoLastStroke()
    {
        if (DrawingCanvas.Strokes.Count > 0)
        {
            var lastStroke = DrawingCanvas.Strokes[^1];
            _undoStack.Add(lastStroke);
            DrawingCanvas.Strokes.RemoveAt(DrawingCanvas.Strokes.Count - 1);
        }
        else if (_imageUndoStack.Count > 0)
        {
            // 이미지 상태 되돌리기 (채우기/모자이크/없애기 undo)
            ContentImage.Source = _imageUndoStack.Pop();
            ShowStatus(Loc.StatusUndone);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        bool cleared = false;
        if (DrawingCanvas.Strokes.Count > 0)
        {
            foreach (var stroke in DrawingCanvas.Strokes)
            {
                _undoStack.Add(stroke);
            }
            DrawingCanvas.Strokes.Clear();
            cleared = true;
        }
        
        // 이미지 편집도 되돌리기
        if (_imageUndoStack.Count > 0)
        {
            // 가장 처음 상태로 복원
            BitmapSource firstState = null!;
            while (_imageUndoStack.Count > 0)
            {
                firstState = _imageUndoStack.Pop();
            }
            ContentImage.Source = firstState;
            cleared = true;
        }
        
        if (cleared)
        {
            ShowStatus(Loc.StatusCleared);
        }
    }

    private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isImageMode && _hasSelection && _originalImage != null)
        {
            // 이미지 영역 선택 복사
            var finalImage = RenderImageWithDrawing();
            var rect = GetImageSpaceSelectionRect();
            
            int x = Math.Max(0, (int)rect.X);
            int y = Math.Max(0, (int)rect.Y);
            int w = Math.Min((int)rect.Width, finalImage.PixelWidth - x);
            int h = Math.Min((int)rect.Height, finalImage.PixelHeight - y);
            
            if (w > 0 && h > 0)
            {
                var cropped = new CroppedBitmap(finalImage, new Int32Rect(x, y, w, h));
                CopyImageToClipboard(cropped);
                ShowStatus(Loc.StatusSelCopiedImage(w, h));
            }
            return;
        }
        
        if (!_isImageMode)
        {
            var selectedText = ContentTextBox.SelectedText;
            if (string.IsNullOrEmpty(selectedText))
            {
                ShowStatus(Loc.StatusNoSelection);
                return;
            }

            CopyToClipboard(selectedText);
            var byteSize = Encoding.UTF8.GetByteCount(selectedText);
            var runeCount = selectedText.EnumerateRunes().Count();
            ShowStatus(Loc.StatusSelCopied(runeCount, FormatSize(byteSize)));
        }
    }

    private void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isImageMode && _originalImage != null)
        {
            if (_isFitToWindow)
            {
                RestoreOriginalSize();
            }
            
            var finalImage = RenderImageWithDrawing();
            CopyImageToClipboard(finalImage);
            Close();
        }
        else
        {
            var text = ContentTextBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                ShowStatus(Loc.StatusNothingToCopy);
                return;
            }

            CopyToClipboard(text);
            Close();
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{bytes:N0} bytes ({size:0.##} {sizes[order]})";
    }

    private BitmapSource RenderImageWithDrawing()
    {
        var currentSource = ContentImage.Source as BitmapSource ?? _originalImage;
        if (currentSource == null) return null!;
        
        var width = _originalImage!.PixelWidth;
        var height = _originalImage.PixelHeight;
        
        var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        
        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            context.DrawImage(currentSource, new WpfRect(0, 0, width, height));
            
            if (Math.Abs(_currentScale - 1.0) > 0.001)
            {
                var ratio = 1.0 / _currentScale;
                foreach (var stroke in DrawingCanvas.Strokes)
                {
                    var cloned = stroke.Clone();
                    var matrix = new Matrix();
                    matrix.Scale(ratio, ratio);
                    cloned.Transform(matrix, false);
                    cloned.Draw(context);
                }
            }
            else
            {
                foreach (var stroke in DrawingCanvas.Strokes)
                {
                    stroke.Draw(context);
                }
            }
        }
        
        renderTarget.Render(drawingVisual);
        return renderTarget;
    }

    private void CopyToClipboard(string text)
    {
        SuppressClipboardMonitor = true;
        try
        {
            WpfClipboard.SetText(text);
        }
        finally
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            timer.Tick += (s, e) =>
            {
                SuppressClipboardMonitor = false;
                timer.Stop();
            };
            timer.Start();
        }
    }

    private void CopyImageToClipboard(BitmapSource image)
    {
        SuppressClipboardMonitor = true;
        try
        {
            WpfClipboard.SetImage(image);
        }
        finally
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            timer.Tick += (s, e) =>
            {
                SuppressClipboardMonitor = false;
                timer.Stop();
            };
            timer.Start();
        }
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        timer.Tick += (s, e) =>
        {
            StatusText.Text = "";
            timer.Stop();
        };
        timer.Start();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isImageMode && _originalImage != null)
        {
            SaveImageFile();
        }
        else
        {
            SaveTextFile();
        }
    }

    private void SaveTextFile()
    {
        var dialog = new WpfSaveFileDialog
        {
            Filter = Loc.TextFileFilter,
            DefaultExt = ".txt",
            FileName = _sourceFilePath != null 
                ? Path.GetFileNameWithoutExtension(_sourceFilePath) + "_copy"
                : $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, ContentTextBox.Text, _currentEncoding);
                ShowStatus(Loc.StatusSaved);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(Loc.SaveFailed(ex.Message), Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveImageFile()
    {
        bool wasFit = _isFitToWindow;
        if (wasFit)
        {
            RestoreOriginalSize();
        }
        
        var finalImage = RenderImageWithDrawing();
        
        if (wasFit)
        {
            _isFitToWindow = true;
            ApplyFitToWindow();
        }
        
        var dialog = new WpfSaveFileDialog
        {
            Filter = Loc.ImageFileFilter,
            DefaultExt = ".png",
            FileName = _sourceFilePath != null 
                ? Path.GetFileNameWithoutExtension(_sourceFilePath) + "_copy"
                : $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}"
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

                encoder.Frames.Add(BitmapFrame.Create(finalImage));
                using var stream = File.OpenWrite(dialog.FileName);
                encoder.Save(stream);
                ShowStatus(Loc.StatusSaved);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(Loc.SaveFailed(ex.Message), Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
