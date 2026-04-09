using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AutoPad.Services;
using AutoPad.Windows;
using Application = System.Windows.Application;

namespace AutoPad;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    static App()
    {
        // EUC-KR 등 코드 페이지 인코딩 지원 등록
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    
    private static Mutex? _mutex;
    private static bool _isMutexOwner;
    private const string MutexName = "AutoPad_SingleInstance_Mutex";
    
    private NotifyIcon? _notifyIcon;
    private ClipboardMonitor? _clipboardMonitor;
    private SettingsService _settingsService = new();
    private ClipboardHistoryService _historyService = new();
    private bool _isSettingsPaused;
    private MainWindow? _hiddenWindow;
    private ToastWindow? _currentToast;
    private SettingsWindow? _settingsWindow;
    private HistoryWindow? _historyWindow;
    private ToolStripMenuItem? _monitoringItem;

    /// <summary>
    /// 전역 설정 서비스 (다른 창에서 접근용)
    /// </summary>
    public static SettingsService? SettingsService { get; private set; }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // 미처리 예외 핸들러 등록
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 중복 실행 방지
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            System.Windows.MessageBox.Show(Loc.AppAlreadyRunning, "AutoPad", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        _isMutexOwner = true;

        _settingsService.Load();
        SettingsService = _settingsService;

        // 히스토리 서비스 로드
        _historyService.MaxItems = _settingsService.Settings.HistoryMaxItems;
        _historyService.Load();

        // 언어 설정 적용
        Loc.Language = _settingsService.Settings.Language;

        // 숨겨진 메인 창 생성 (클립보드 모니터링용)
        _hiddenWindow = new MainWindow();
        _hiddenWindow.WindowState = WindowState.Minimized;
        _hiddenWindow.ShowInTaskbar = false;
        _hiddenWindow.Visibility = Visibility.Hidden;
        _hiddenWindow.Show();

        // 트레이 아이콘 설정
        SetupNotifyIcon();

        // 클립보드 모니터링 시작
        SetupClipboardMonitor();

        // 최초 실행 시 설정 창 열기
        if (_settingsService.IsFirstRun)
        {
            _settingsService.Save();
            OpenSettingsWindow();
        }
        else if (!_settingsService.Settings.StartMinimized)
        {
            OpenSettingsWindow();
        }
    }

    private void SetupNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = Loc.TrayTooltip,
            Visible = true
        };

        // 커스텀 아이콘 생성
        _notifyIcon.Icon = IconHelper.CreateAppIcon(32);

        // 컨텍스트 메뉴 생성
        var contextMenu = new ContextMenuStrip();
        
        _monitoringItem = new ToolStripMenuItem(Loc.TrayMonitoring)
        {
            Checked = _settingsService.Settings.IsMonitoringEnabled,
            CheckOnClick = true
        };
        _monitoringItem.CheckedChanged += (s, e) =>
        {
            _settingsService.Settings.IsMonitoringEnabled = _monitoringItem.Checked;
            _settingsService.Save();
        };
        contextMenu.Items.Add(_monitoringItem);

        var historyItem = new ToolStripMenuItem(Loc.TrayHistory);
        historyItem.Click += (s, e) => Dispatcher.Invoke(() => OpenHistoryWindow());
        contextMenu.Items.Add(historyItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem(Loc.TraySettings);
        settingsItem.Click += (s, e) => OpenSettingsWindow();
        contextMenu.Items.Add(settingsItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem(Loc.TrayExit);
        exitItem.Click += (s, e) => Shutdown();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        // 더블클릭 시 설정 창 열기
        _notifyIcon.DoubleClick += (s, e) => OpenSettingsWindow();
    }

    private void OpenSettingsWindow()
    {
        // 이미 열려있는 설정 창이 있으면 활성화
        if (_settingsWindow != null && _settingsWindow.IsLoaded)
        {
            _settingsWindow.Activate();
            _settingsWindow.Focus();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsService);
        _isSettingsPaused = true;
        _settingsWindow.Closed += (s, e) =>
        {
            _isSettingsPaused = false;
            _settingsWindow = null;
            if (_monitoringItem != null)
            {
                _monitoringItem.Checked = _settingsService.Settings.IsMonitoringEnabled;
            }
            // 파일 모니터링 설정 즉시 반영
            if (_clipboardMonitor != null)
            {
                _clipboardMonitor.IsFileMonitoringEnabled = _settingsService.Settings.IsFileMonitoringEnabled;
                _clipboardMonitor.IsImageMonitoringEnabled = _settingsService.Settings.IsImageMonitoringEnabled;
                _clipboardMonitor.MaxFileSizeBytes = _settingsService.Settings.FileMonitoringMaxSizeMB * 1024L * 1024L;
            }
        };
        _settingsWindow.Show();
    }

    private void OpenHistoryWindow()
    {
        if (_historyWindow != null && _historyWindow.IsLoaded)
        {
            _historyWindow.Activate();
            _historyWindow.Focus();
            return;
        }

        _historyWindow = new HistoryWindow(_historyService);
        _historyWindow.Closed += (s, e) => _historyWindow = null;
        _historyWindow.Show();
    }

    private void SetupClipboardMonitor()
    {
        _clipboardMonitor = new ClipboardMonitor();
        _clipboardMonitor.ClipboardChanged += OnClipboardChanged;
        
        // 파일 모니터링 설정 적용
        _clipboardMonitor.IsFileMonitoringEnabled = _settingsService.Settings.IsFileMonitoringEnabled;
        _clipboardMonitor.IsImageMonitoringEnabled = _settingsService.Settings.IsImageMonitoringEnabled;
        _clipboardMonitor.MaxFileSizeBytes = _settingsService.Settings.FileMonitoringMaxSizeMB * 1024L * 1024L;
        
        if (_hiddenWindow != null)
        {
            _clipboardMonitor.Start(_hiddenWindow);
        }
    }

    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        // 모니터링 비활성화 상태면 무시
        if (!_settingsService.Settings.IsMonitoringEnabled)
            return;

        // 설정 창이 열려있는 동안 일시 중단
        if (_isSettingsPaused)
            return;

        // 편집 창에서 복사한 경우 무시
        if (EditWindow.SuppressClipboardMonitor)
            return;

        // 기존 토스트 닫기
        _currentToast?.Close();

        Dispatcher.Invoke(() =>
        {
            var settings = _settingsService.Settings;

            if (e.ContentType == ClipboardContentType.Text && e.Content is string text)
            {
                if (settings.IsHistoryEnabled)
                    _historyService.AddText(text, Encoding.UTF8.GetByteCount(text));

                _currentToast = new ToastWindow(text, settings.ToastPosition, settings.ToastDurationSeconds, settings.IsCompactMode, e.HtmlContent);
                _currentToast.Show();
            }
            else if (e.ContentType == ClipboardContentType.Image && e.Content is BitmapSource image)
            {
                if (settings.IsHistoryEnabled)
                    _historyService.AddImage(image);

                _currentToast = new ToastWindow(image, settings.ToastPosition, settings.ToastDurationSeconds, settings.IsCompactMode);
                _currentToast.Show();
            }
            else if (e.ContentType == ClipboardContentType.File && e.Content is string filePath)
            {
                if (settings.IsHistoryEnabled)
                    _historyService.AddFile(filePath);

                _currentToast = new ToastWindow(filePath, isFile: true, settings.ToastPosition, settings.ToastDurationSeconds, settings.IsCompactMode);
                _currentToast.Show();
            }
        });
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _historyService.Save();
        _clipboardMonitor?.Dispose();
        _notifyIcon?.Dispose();
        if (_isMutexOwner)
        {
            _mutex?.ReleaseMutex();
        }
        _mutex?.Dispose();
    }

    // ── 미처리 예외 핸들러 ──

    private static string CrashLogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoPad", "crash.log");

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteCrashLog(ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        e.SetObserved();
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(CrashLogPath)!;
            Directory.CreateDirectory(dir);
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n---\n";
            File.AppendAllText(CrashLogPath, entry);
        }
        catch
        {
            // 로그 기록 실패는 무시
        }
    }
}

