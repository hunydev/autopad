using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
    private CancellationTokenSource? _textLoadCts;

    private const int AsyncTextThresholdChars = 256 * 1024;
    private const int TextAppendChunkSize = 128 * 1024;

    // 영역 선택 관련
    private bool _isSelecting = false;
    private bool _isSelectMode = false;
    private WpfPoint _selectionStart;
    private WpfRect _selectionRect;
    private bool _hasSelection = false;

    // Undo 지원: 이미지 상태 스택
    private readonly Stack<BitmapSource> _imageUndoStack = new();

    public static bool SuppressClipboardMonitor { get; internal set; }

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
        RemoveLinesButton.Content = Loc.BtnRemoveLines;
        RemoveBlankLinesButton.Content = Loc.BtnRemoveBlankLines;
        RemoveAllLinesButton.Content = Loc.BtnRemoveAllLines;
        ReplaceWhitespaceButton.Content = Loc.BtnReplaceWhitespace;
        WhitespaceDeleteButton.Content = Loc.BtnWhitespaceDelete;
        WhitespaceReplaceButton.Content = Loc.BtnWhitespaceReplaceSpace;
        TrimButton.Content = Loc.BtnTrim;
        MaskNumbersButton.Content = Loc.BtnMaskNumbers;
        UpperCaseButton.Content = Loc.BtnUpperCase;
        LowerCaseButton.Content = Loc.BtnLowerCase;
        MacroButton.Content = Loc.BtnMacro;
        MacroEmptyText.Text = Loc.MacroEmpty;
        PinButtonText.Text = Loc.BtnPin;
        TextLoadingMessage.Text = Loc.LoadingText;
        BinaryWarningTitle.Text = Loc.BinaryWarningTitle;
        BinaryWarningDesc.Text = Loc.BinaryWarningDesc;
        ViewAsTextButtonText.Text = Loc.BtnViewAsText;
    }

    private void ApplySpellCheck()
    {
        if (App.SettingsService?.Settings.IsSpellCheckEnabled == true)
        {
            ContentTextBox.SpellCheck.IsEnabled = true;
        }
    }

    public EditWindow(string text, bool isDarkMode = true)
    {
        InitializeComponent();
        ApplyLocalization();
        _isImageMode = false;
        Closed += (_, _) => _textLoadCts?.Cancel();
        
        Icon = IconHelper.CreateAppIconImageSource(32);
        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, isDarkMode);

        ContentTextBox.SelectionChanged += ContentTextBox_SelectionChanged;
        ContentTextBox.PreviewMouseMove += ContentTextBox_PreviewMouseMove;
        ContentTextBox.MouseLeave += ContentTextBox_MouseLeave;
        
        TextToolbar.Visibility = Visibility.Visible;
        ApplySpellCheck();

        if (text.Length >= AsyncTextThresholdChars)
        {
            Title = "AutoPad - Edit";
            _ = LoadTextAsync(
                loader: ct => Task.Run(() => BuildTextLoadResult(text, Encoding.UTF8, ct), ct),
                loadingMessage: Loc.LoadingText,
                focusAndSelectAll: true);
        }
        else
        {
            ApplyLoadedText(text, BuildTextMeta(text, Encoding.UTF8), true);
        }
        
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
        Base64CopyButton.Content = Loc.BtnBase64Copy;
        Base64CopyButton.Visibility = Visibility.Visible;
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
        Closed += (_, _) => _textLoadCts?.Cancel();
        
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
                Base64CopyButton.Content = Loc.BtnBase64Copy;
                Base64CopyButton.Visibility = Visibility.Visible;
                SetupSelectionEvents();
                SizeChanged += OnWindowSizeChanged;
            }
            catch (Exception ex)
            {
                ShowStatus(Loc.ImageLoadFailed(ex.Message));
                _isImageMode = false;
                _ = LoadTextAsync(
                    loader: ct => Task.Run(() => BuildTextLoadResultFromFile(filePath, _currentEncoding, ct), ct),
                    loadingMessage: Loc.LoadingFile(Path.GetFileName(filePath)));
            }
        }
        else
        {
            _isImageMode = false;
            EncodingToolbar.Visibility = Visibility.Visible;
            TextToolbar.Visibility = Visibility.Visible;
            ApplySpellCheck();
            ContentTextBox.SelectionChanged += ContentTextBox_SelectionChanged;
            ContentTextBox.PreviewMouseMove += ContentTextBox_PreviewMouseMove;
            ContentTextBox.MouseLeave += ContentTextBox_MouseLeave;

            _ = LoadTextAsync(
                loader: ct => Task.Run(() => BuildTextLoadResultFromFile(filePath, _currentEncoding, ct), ct),
                loadingMessage: Loc.LoadingFile(Path.GetFileName(filePath)));
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
            Base64CopyButton.Content = Loc.BtnBase64CopySelection;
            PinButtonText.Text = Loc.BtnPinSelection;
        }
        else
        {
            _hasSelection = false;
            SelectionRectangle.Visibility = Visibility.Collapsed;
            CopySelectedButton.Visibility = Visibility.Collapsed;
            Base64CopyButton.Content = Loc.BtnBase64Copy;
        }
    }

    private sealed class TextMeta
    {
        public int RuneCount { get; init; }
        public int ByteSize { get; init; }
        public int LineCount { get; init; }
    }

    private sealed class TextLoadResult
    {
        public string Text { get; init; } = string.Empty;
        public TextMeta Meta { get; init; } = new();
        public string? Error { get; init; }
        public bool IsBinary { get; init; }
    }

    private TextMeta BuildTextMeta(string text, Encoding encoding)
    {
        var lineCount = 1;
        foreach (var ch in text)
        {
            if (ch == '\n') lineCount++;
        }

        return new TextMeta
        {
            RuneCount = text.EnumerateRunes().Count(),
            ByteSize = encoding.GetByteCount(text),
            LineCount = lineCount
        };
    }

    private TextLoadResult BuildTextLoadResult(string text, Encoding encoding, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var meta = BuildTextMeta(text, encoding);
        return new TextLoadResult { Text = text, Meta = meta };
    }

    private static bool IsBinaryContent(byte[] bytes)
    {
        var checkLength = Math.Min(bytes.Length, 8192);
        for (var i = 0; i < checkLength; i++)
        {
            if (bytes[i] == 0x00) return true;
        }
        return false;
    }

    private TextLoadResult BuildTextLoadResultFromFile(string filePath, Encoding encoding, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                return new TextLoadResult { Error = Loc.FileNotFound };
            }

            var bytes = File.ReadAllBytes(filePath);
            ct.ThrowIfCancellationRequested();

            if (IsBinaryContent(bytes))
            {
                return new TextLoadResult { IsBinary = true };
            }

            var text = encoding.GetString(bytes);
            ct.ThrowIfCancellationRequested();

            var meta = BuildTextMeta(text, encoding);
            return new TextLoadResult { Text = text, Meta = meta };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TextLoadResult { Error = Loc.FileReadFailed(ex.Message) };
        }
    }

    private void SetTextLoadingState(bool isLoading, string? loadingMessage = null)
    {
        TextLoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (!string.IsNullOrWhiteSpace(loadingMessage))
        {
            TextLoadingMessage.Text = loadingMessage;
        }

        TextToolbar.IsEnabled = !isLoading;
        EncodingToolbar.IsEnabled = !isLoading;
        CopyAllButton.IsEnabled = !isLoading;
        CopySelectedButton.IsEnabled = !isLoading;
        SaveButton.IsEnabled = !isLoading;
        PinButton.IsEnabled = !isLoading;
    }

    private async Task ApplyTextInChunksAsync(string text, CancellationToken ct)
    {
        ContentTextBox.Clear();
        if (string.IsNullOrEmpty(text)) return;

        for (var i = 0; i < text.Length; i += TextAppendChunkSize)
        {
            ct.ThrowIfCancellationRequested();
            var len = Math.Min(TextAppendChunkSize, text.Length - i);
            ContentTextBox.AppendText(text.Substring(i, len));
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }

    private void ApplyLoadedText(string text, TextMeta meta, bool selectAll)
    {
        ContentTextBox.Text = text;
        Title = Loc.EditTitle(meta.RuneCount, FormatSize(meta.ByteSize), meta.LineCount);
        ContentTextBox.Focus();
        if (selectAll)
        {
            ContentTextBox.SelectAll();
        }
    }

    private Func<CancellationToken, Task<TextLoadResult>>? _pendingBinaryLoader;
    private bool _pendingBinaryFocusAndSelectAll;

    private async Task LoadTextAsync(Func<CancellationToken, Task<TextLoadResult>> loader, string loadingMessage, bool focusAndSelectAll = false)
    {
        _textLoadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _textLoadCts = cts;

        SetTextLoadingState(true, loadingMessage);

        try
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            var result = await loader(cts.Token);

            if (!ReferenceEquals(_textLoadCts, cts)) return;

            if (result.IsBinary)
            {
                _pendingBinaryLoader = loader;
                _pendingBinaryFocusAndSelectAll = focusAndSelectAll;
                SetTextLoadingState(false);
                ShowBinaryWarning(true);
                return;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                ContentTextBox.Text = result.Error;
                return;
            }

            await ApplyTextInChunksAsync(result.Text, cts.Token);
            Title = Loc.EditTitle(result.Meta.RuneCount, FormatSize(result.Meta.ByteSize), result.Meta.LineCount);
            ContentTextBox.Focus();
            if (focusAndSelectAll)
            {
                ContentTextBox.SelectAll();
            }
        }
        catch (OperationCanceledException)
        {
            // 최신 요청으로 교체될 수 있어 취소는 정상 동작이다.
        }
        finally
        {
            if (ReferenceEquals(_textLoadCts, cts))
            {
                SetTextLoadingState(false);
                _textLoadCts = null;
            }
        }
    }

    private void ShowBinaryWarning(bool show)
    {
        BinaryWarningOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ContentTextBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void ViewAsTextButton_Click(object sender, RoutedEventArgs e)
    {
        ShowBinaryWarning(false);
        if (_pendingBinaryLoader == null || _sourceFilePath == null) return;

        var filePath = _sourceFilePath;
        var encoding = _currentEncoding;
        var focusAndSelectAll = _pendingBinaryFocusAndSelectAll;
        _pendingBinaryLoader = null;

        await LoadTextAsync(
            loader: ct => Task.Run(() => BuildTextLoadResultFromFileForce(filePath, encoding, ct), ct),
            loadingMessage: Loc.LoadingFile(Path.GetFileName(filePath)),
            focusAndSelectAll: focusAndSelectAll);
    }

    private TextLoadResult BuildTextLoadResultFromFileForce(string filePath, Encoding encoding, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(filePath))
                return new TextLoadResult { Error = Loc.FileNotFound };

            var bytes = File.ReadAllBytes(filePath);
            ct.ThrowIfCancellationRequested();

            var text = encoding.GetString(bytes);
            ct.ThrowIfCancellationRequested();

            var meta = BuildTextMeta(text, encoding);
            return new TextLoadResult { Text = text, Meta = meta };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new TextLoadResult { Error = Loc.FileReadFailed(ex.Message) };
        }
    }

    private bool _isChangingEncoding = false;

    private async void EncodingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                await LoadTextAsync(
                    loader: ct => Task.Run(() => BuildTextLoadResultFromFile(filePath, encoding, ct), ct),
                    loadingMessage: Loc.LoadingFile(Path.GetFileName(filePath)));
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
            if (UrlPopup.IsOpen)
            {
                HideUrlPopup();
                e.Handled = true;
                return;
            }
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
        if (_isImageMode)
        {
            Base64CopyButton.Content = Loc.BtnBase64Copy;
            PinButtonText.Text = Loc.BtnPin;
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

    private void Base64CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isImageMode || _originalImage == null) return;

        BitmapSource source;
        bool isSelection = false;

        if (_hasSelection)
        {
            var finalImage = RenderImageWithDrawing();
            var rect = GetImageSpaceSelectionRect();
            int x = Math.Max(0, (int)rect.X);
            int y = Math.Max(0, (int)rect.Y);
            int w = Math.Min((int)rect.Width, finalImage.PixelWidth - x);
            int h = Math.Min((int)rect.Height, finalImage.PixelHeight - y);
            if (w <= 0 || h <= 0) return;
            source = new CroppedBitmap(finalImage, new Int32Rect(x, y, w, h));
            isSelection = true;
        }
        else
        {
            source = RenderImageWithDrawing();
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var dataUri = $"data:image/png;base64,{base64}";

        SuppressClipboardMonitor = true;
        WpfClipboard.SetText(dataUri);
        SuppressClipboardMonitor = false;

        ShowStatus(isSelection ? Loc.StatusBase64SelectionCopied : Loc.StatusBase64Copied);
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isImageMode && _originalImage != null)
        {
            BitmapSource pinImage;
            if (_hasSelection)
            {
                var finalImage = RenderImageWithDrawing();
                var rect = GetImageSpaceSelectionRect();
                int x = Math.Max(0, (int)rect.X);
                int y = Math.Max(0, (int)rect.Y);
                int w = Math.Min((int)rect.Width, finalImage.PixelWidth - x);
                int h = Math.Min((int)rect.Height, finalImage.PixelHeight - y);
                if (w <= 0 || h <= 0) return;
                pinImage = new CroppedBitmap(finalImage, new Int32Rect(x, y, w, h));
            }
            else
            {
                pinImage = RenderImageWithDrawing();
            }
            var sticky = new StickyWindow(pinImage);
            sticky.Show();
        }
        else
        {
            string pinText;
            if (!string.IsNullOrEmpty(ContentTextBox.SelectedText))
            {
                pinText = ContentTextBox.SelectedText;
            }
            else
            {
                pinText = ContentTextBox.Text;
            }
            if (string.IsNullOrEmpty(pinText)) return;
            var sticky = new StickyWindow(pinText);
            sticky.Show();
        }
        Close();
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
                using var stream = File.Create(dialog.FileName);
                encoder.Save(stream);
                ShowStatus(Loc.StatusSaved);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(Loc.SaveFailed(ex.Message), Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ── 텍스트 선택 정보 표시 ──

    private void ContentTextBox_SelectionChanged(object? sender, RoutedEventArgs e)
    {
        var selected = ContentTextBox.SelectedText;
        if (string.IsNullOrEmpty(selected))
        {
            // 전체 텍스트의 메타 정보 표시
            var fullText = ContentTextBox.Text;
            var runeCount = fullText.EnumerateRunes().Count();
            var byteSize = Encoding.UTF8.GetByteCount(fullText);
            var lineCount = fullText.Split('\n').Length;
            SelectionInfoText.Text = Loc.SelectionInfoFull(runeCount, FormatSize(byteSize), lineCount);
            PinButtonText.Text = Loc.BtnPin;
        }
        else
        {
            var runeCount = selected.EnumerateRunes().Count();
            var byteSize = Encoding.UTF8.GetByteCount(selected);
            var lineCount = selected.Split('\n').Length;
            SelectionInfoText.Text = Loc.SelectionInfoSelected(runeCount, FormatSize(byteSize), lineCount);
            PinButtonText.Text = Loc.BtnPinSelection;
        }
    }

    // ── 라인 제거 ──

    private void RemoveLinesButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveLinesPopup.IsOpen = !RemoveLinesPopup.IsOpen;
    }

    private void RemoveBlankLinesButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveLinesPopup.IsOpen = false;

        if (ContentTextBox.SelectionLength > 0)
        {
            var selected = ContentTextBox.SelectedText;
            var lines = selected.Split('\n');
            var filtered = lines.Where(l => l.TrimEnd('\r').Trim().Length > 0);
            var result = string.Join("\n", filtered);
            ReplaceSelection(result);
        }
        else
        {
            var lines = ContentTextBox.Text.Split('\n');
            var filtered = lines.Where(l => l.TrimEnd('\r').Trim().Length > 0);
            ContentTextBox.Text = string.Join("\n", filtered);
        }
        UpdateTitleMeta();
        ShowStatus(Loc.StatusBlankLinesRemoved);
    }

    private void RemoveAllLinesButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveLinesPopup.IsOpen = false;

        if (ContentTextBox.SelectionLength > 0)
        {
            var selected = ContentTextBox.SelectedText;
            var result = selected.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
            ReplaceSelection(result);
        }
        else
        {
            ContentTextBox.Text = ContentTextBox.Text.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
        }
        UpdateTitleMeta();
        ShowStatus(Loc.StatusAllLinesRemoved);
    }

    // ── 특수공백 제거 ──

    private void ReplaceWhitespaceButton_Click(object sender, RoutedEventArgs e)
    {
        WhitespacePopup.IsOpen = !WhitespacePopup.IsOpen;
    }

    private void WhitespaceDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        WhitespacePopup.IsOpen = false;
        ProcessSpecialWhitespace(replaceWithSpace: false);
    }

    private void WhitespaceReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        WhitespacePopup.IsOpen = false;
        ProcessSpecialWhitespace(replaceWithSpace: true);
    }

    private void ProcessSpecialWhitespace(bool replaceWithSpace)
    {
        RemoveLinesPopup.IsOpen = false;
        var text = ContentTextBox.SelectionLength > 0 ? ContentTextBox.SelectedText : ContentTextBox.Text;

        var counts = new Dictionary<string, int>();
        var sb = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var cat = Rune.GetUnicodeCategory(rune);
            bool isNonAsciiWhitespace = (cat == UnicodeCategory.SpaceSeparator
                || cat == UnicodeCategory.LineSeparator
                || cat == UnicodeCategory.ParagraphSeparator
                || rune.Value == 0x00A0  // NBSP
                || rune.Value == 0x200B  // Zero-width space
                || rune.Value == 0xFEFF  // BOM/ZWNBSP
                || rune.Value == 0x200C  // ZWNJ
                || rune.Value == 0x200D  // ZWJ
                || rune.Value == 0x2060  // Word Joiner
                ) && rune.Value > 127;
            if (isNonAsciiWhitespace)
            {
                var name = $"U+{rune.Value:X4}";
                counts[name] = counts.GetValueOrDefault(name) + 1;
                if (replaceWithSpace)
                    sb.Append(' ');
            }
            else
            {
                sb.Append(rune.ToString());
            }
        }

        if (counts.Count == 0)
        {
            ShowStatus(Loc.StatusNoWhitespaceFound);
            return;
        }

        if (ContentTextBox.SelectionLength > 0)
            ReplaceSelection(sb.ToString());
        else
            ContentTextBox.Text = sb.ToString();

        var detail = string.Join("\n", counts.Select(kv => $"  {kv.Key}: {kv.Value}"));
        var total = counts.Values.Sum();
        System.Windows.MessageBox.Show(
            replaceWithSpace ? Loc.MsgWhitespaceReplacedWithSpace(total, detail) : Loc.MsgWhitespaceReplaced(total, detail),
            "AutoPad", MessageBoxButton.OK, MessageBoxImage.Information);
        UpdateTitleMeta();
    }

    // ── TRIM ──

    private void TrimButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveLinesPopup.IsOpen = false;

        if (ContentTextBox.SelectionLength > 0)
        {
            var selected = ContentTextBox.SelectedText;
            var lines = selected.Split('\n');
            var trimmed = lines.Select(l =>
            {
                var cr = l.EndsWith('\r') ? "\r" : "";
                return l.TrimEnd('\r').Trim() + cr;
            });
            ReplaceSelection(string.Join("\n", trimmed));
        }
        else
        {
            var lines = ContentTextBox.Text.Split('\n');
            var trimmed = lines.Select(l =>
            {
                var cr = l.EndsWith('\r') ? "\r" : "";
                return l.TrimEnd('\r').Trim() + cr;
            });
            ContentTextBox.Text = string.Join("\n", trimmed);
        }
        UpdateTitleMeta();
        ShowStatus(Loc.StatusTrimmed);
    }

    // ── 숫자 마스킹 ──

    private void MaskNumbersButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveLinesPopup.IsOpen = false;
        var text = ContentTextBox.SelectionLength > 0 ? ContentTextBox.SelectedText : ContentTextBox.Text;

        int count = 0;
        var result = Regex.Replace(text, @"\d", m => { count++; return "*"; });

        if (count == 0)
        {
            ShowStatus(Loc.StatusNoNumbersFound);
            return;
        }

        if (ContentTextBox.SelectionLength > 0)
            ReplaceSelection(result);
        else
            ContentTextBox.Text = result;

        System.Windows.MessageBox.Show(
            Loc.MsgNumbersMasked(count),
            "AutoPad", MessageBoxButton.OK, MessageBoxImage.Information);
        UpdateTitleMeta();
    }

    // ── 대문자 / 소문자 ──

    private void UpperCaseButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveLinesPopup.IsOpen = false;
        if (ContentTextBox.SelectionLength > 0)
            ReplaceSelection(ContentTextBox.SelectedText.ToUpperInvariant());
        else
            ContentTextBox.Text = ContentTextBox.Text.ToUpperInvariant();
        UpdateTitleMeta();
        ShowStatus(Loc.StatusUpperCased);
    }

    private void LowerCaseButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveLinesPopup.IsOpen = false;
        if (ContentTextBox.SelectionLength > 0)
            ReplaceSelection(ContentTextBox.SelectedText.ToLowerInvariant());
        else
            ContentTextBox.Text = ContentTextBox.Text.ToLowerInvariant();
        UpdateTitleMeta();
        ShowStatus(Loc.StatusLowerCased);
    }

    // ── 매크로 ──

    private void MacroButton_Click(object sender, RoutedEventArgs e)
    {
        BuildMacroPopup();
        MacroPopup.IsOpen = !MacroPopup.IsOpen;
        if (MacroPopup.IsOpen)
        {
            MacroSearchBox.Text = "";
            MacroSearchBox.Focus();
        }
    }

    private void MacroSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterMacroPopup();
    }

    private void FilterMacroPopup()
    {
        var query = MacroSearchBox.Text.Trim();
        int visibleCount = 0;

        foreach (var child in MacroPopupPanel.Children)
        {
            if (child is WpfButton btn)
            {
                var name = btn.Content?.ToString() ?? "";
                bool match = string.IsNullOrEmpty(query)
                    || name.Contains(query, StringComparison.OrdinalIgnoreCase);
                btn.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                if (match) visibleCount++;
            }
        }

        MacroEmptyText.Visibility = visibleCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        MacroEmptyText.Text = string.IsNullOrEmpty(query) ? Loc.MacroEmpty : Loc.MacroNoResults;
    }

    private void BuildMacroPopup()
    {
        // MacroEmptyText 이후의 동적 버튼 제거
        while (MacroPopupPanel.Children.Count > 1)
            MacroPopupPanel.Children.RemoveAt(MacroPopupPanel.Children.Count - 1);

        var macros = App.SettingsService?.Settings.Macros;
        if (macros == null || macros.Count == 0)
        {
            MacroEmptyText.Visibility = Visibility.Visible;
            return;
        }

        MacroEmptyText.Visibility = Visibility.Collapsed;

        foreach (var macro in macros)
        {
            var btn = new WpfButton
            {
                Content = macro.Name,
                Tag = macro.Id,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 12
            };
            btn.Click += MacroItem_Click;
            MacroPopupPanel.Children.Add(btn);
        }
    }

    private void MacroItem_Click(object sender, RoutedEventArgs e)
    {
        MacroPopup.IsOpen = false;
        if (sender is not WpfButton btn) return;
        var macroId = btn.Tag?.ToString();
        if (macroId == null) return;

        var macro = App.SettingsService?.Settings.Macros.Find(m => m.Id == macroId);
        if (macro == null) return;

        var text = ContentTextBox.SelectionLength > 0 ? ContentTextBox.SelectedText : ContentTextBox.Text;

        try
        {
            var result = MacroService.RunMacro(macro.Script, text);

            if (ContentTextBox.SelectionLength > 0)
                ReplaceSelection(result);
            else
                ContentTextBox.Text = result;

            UpdateTitleMeta();
            ShowStatus(Loc.MacroApplied(macro.Name));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                Loc.MacroRunError(macro.Name, ex.Message),
                "AutoPad", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── URL 호버 버튼 ──

    private static readonly Regex UrlRegex = new(@"https?://[^\s<>""')\]]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private string? _hoveredUrl;
    private string? _shownForUrl;
    private System.Windows.Threading.DispatcherTimer? _urlHideTimer;

    private void CancelUrlHideTimer()
    {
        if (_urlHideTimer != null)
        {
            _urlHideTimer.Stop();
            _urlHideTimer = null;
        }
    }

    private void HideUrlPopup()
    {
        CancelUrlHideTimer();
        UrlPopup.IsOpen = false;
        _hoveredUrl = null;
        _shownForUrl = null;
    }

    private void ScheduleUrlHide()
    {
        CancelUrlHideTimer();
        _urlHideTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _urlHideTimer.Tick += (s, e) =>
        {
            CancelUrlHideTimer();
            if (!UrlOpenButton.IsMouseOver)
                HideUrlPopup();
        };
        _urlHideTimer.Start();
    }

    private void ContentTextBox_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        var pos = e.GetPosition(ContentTextBox);
        var index = ContentTextBox.GetCharacterIndexFromPoint(pos, true);
        if (index >= 0)
        {
            var url = GetUrlAtIndex(ContentTextBox.Text, index);
            if (url != null)
            {
                _hoveredUrl = url;
                CancelUrlHideTimer();

                if (!UrlPopup.IsOpen || _shownForUrl != url)
                {
                    var rect = ContentTextBox.GetRectFromCharacterIndex(index);
                    UrlPopup.HorizontalOffset = rect.Left;
                    UrlPopup.VerticalOffset = rect.Bottom + 2;
                    UrlPopup.IsOpen = true;
                    _shownForUrl = url;
                }
                return;
            }
        }
        if (UrlPopup.IsOpen && !UrlOpenButton.IsMouseOver)
            ScheduleUrlHide();
    }

    private void ContentTextBox_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        if (UrlPopup.IsOpen && !UrlOpenButton.IsMouseOver)
            ScheduleUrlHide();
    }

    private void UrlOpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hoveredUrl != null)
        {
            try { Process.Start(new ProcessStartInfo(_hoveredUrl) { UseShellExecute = true }); }
            catch { }
        }
        HideUrlPopup();
    }

    private void UrlOpenButton_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        ScheduleUrlHide();
    }

    private string? _urlCacheText;
    private MatchCollection? _urlCacheMatches;

    private string? GetUrlAtIndex(string text, int index)
    {
        if (_urlCacheText != text)
        {
            _urlCacheText = text;
            _urlCacheMatches = UrlRegex.Matches(text);
        }
        foreach (Match match in _urlCacheMatches!)
        {
            if (index >= match.Index && index < match.Index + match.Length)
                return match.Value;
        }
        return null;
    }

    // ── 유틸리티 ──

    private void ReplaceSelection(string newText)
    {
        var start = ContentTextBox.SelectionStart;
        var length = ContentTextBox.SelectionLength;
        var full = ContentTextBox.Text;
        ContentTextBox.Text = full[..start] + newText + full[(start + length)..];
        ContentTextBox.SelectionStart = start;
        ContentTextBox.SelectionLength = newText.Length;
    }

    private void UpdateTitleMeta()
    {
        var text = ContentTextBox.Text;
        var runeCount = text.EnumerateRunes().Count();
        var byteSize = Encoding.UTF8.GetByteCount(text);
        var lineCount = text.Split('\n').Length;
        Title = Loc.EditTitle(runeCount, FormatSize(byteSize), lineCount);
    }
}
