using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using AutoPad.Models;
using AutoPad.Services;
using Microsoft.Win32;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace AutoPad.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AutoPad";

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        
        // 아이콘 설정
        Icon = IconHelper.CreateAppIconImageSource(32);
        
        // 다크 모드 제목 표시줄 적용
        SourceInitialized += (s, e) => 
            ThemeHelper.ApplyDarkTitleBar(this, _settingsService.Settings.IsDarkMode);
        
        ApplyLocalization();
        LoadSettings();
    }

    private void ApplyLocalization()
    {
        Title = Loc.SettingsTitle;
        SettingsHeaderText.Text = Loc.SettingsHeader;
        LanguageLabelText.Text = Loc.SettingsLanguage;
        PositionLabelText.Text = Loc.SettingsToastPosition;
        DurationLabelText.Text = Loc.SettingsDuration;
        MonitoringCheckBox.Content = Loc.SettingsEnableMonitoring;
        FileMonitoringCheckBox.Content = Loc.SettingsEnableFileMonitoring;
        FileSizeLimitLabel.Text = Loc.SettingsFileSizeLimit;
        StartWithWindowsCheckBox.Content = Loc.SettingsAutoStart;
        StartMinimizedCheckBox.Content = Loc.SettingsStartMinimized;
        SettingsSaveButton.Content = Loc.SettingsBtnSave;

        // 콤보박스 아이템은 Tag 기반 선택이므로 Content만 변경
        PosBottom.Content = Loc.PosBottomCenter;
        PosTop.Content = Loc.PosTopCenter;
        PosBottomLeft.Content = Loc.PosBottomLeft;
        PosBottomRight.Content = Loc.PosBottomRight;
        PosTopLeft.Content = Loc.PosTopLeft;
        PosTopRight.Content = Loc.PosTopRight;

        Dur3.Content = Loc.Duration(3);
        Dur5.Content = Loc.Duration(5);
        Dur10.Content = Loc.Duration(10);
        Dur15.Content = Loc.Duration(15);
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Settings;

        // 위치 선택
        foreach (WpfComboBoxItem item in PositionComboBox.Items)
        {
            if (item.Tag?.ToString() == settings.ToastPosition.ToString())
            {
                PositionComboBox.SelectedItem = item;
                break;
            }
        }

        // 표시 시간 선택
        foreach (WpfComboBoxItem item in DurationComboBox.Items)
        {
            if (item.Tag?.ToString() == settings.ToastDurationSeconds.ToString())
            {
                DurationComboBox.SelectedItem = item;
                break;
            }
        }

        MonitoringCheckBox.IsChecked = settings.IsMonitoringEnabled;
        FileMonitoringCheckBox.IsChecked = settings.IsFileMonitoringEnabled;
        UpdateFileMonitoringEnabled();
        
        // 파일 크기 제한 선택
        foreach (WpfComboBoxItem item in FileSizeComboBox.Items)
        {
            if (item.Tag?.ToString() == settings.FileMonitoringMaxSizeMB.ToString())
            {
                FileSizeComboBox.SelectedItem = item;
                break;
            }
        }
        if (FileSizeComboBox.SelectedItem == null && FileSizeComboBox.Items.Count > 0)
        {
            FileSizeComboBox.SelectedIndex = 2; // 기본값 10 MB
        }
        
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        StartMinimizedCheckBox.IsChecked = settings.StartMinimized;

        // 언어 선택
        LanguageComboBox.SelectedIndex = settings.Language == "ko" ? 1 : 0;
    }

    private void MonitoringCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateFileMonitoringEnabled();
    }

    private void UpdateFileMonitoringEnabled()
    {
        bool enabled = MonitoringCheckBox.IsChecked ?? false;
        FileMonitoringCheckBox.IsEnabled = enabled;
        FileSizeComboBox.IsEnabled = enabled;
        FileSizeLimitLabel.IsEnabled = enabled;
        if (!enabled)
        {
            FileMonitoringCheckBox.IsChecked = false;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Settings;

        // 위치 저장
        if (PositionComboBox.SelectedItem is WpfComboBoxItem posItem)
        {
            if (Enum.TryParse<ToastPosition>(posItem.Tag?.ToString(), out var position))
            {
                settings.ToastPosition = position;
            }
        }

        // 표시 시간 저장
        if (DurationComboBox.SelectedItem is WpfComboBoxItem durItem)
        {
            if (int.TryParse(durItem.Tag?.ToString(), out var duration))
            {
                settings.ToastDurationSeconds = duration;
            }
        }

        settings.IsMonitoringEnabled = MonitoringCheckBox.IsChecked ?? true;
        settings.IsFileMonitoringEnabled = FileMonitoringCheckBox.IsChecked ?? true;
        
        // 파일 크기 제한 저장
        if (FileSizeComboBox.SelectedItem is WpfComboBoxItem sizeItem)
        {
            if (int.TryParse(sizeItem.Tag?.ToString(), out var maxSize))
            {
                settings.FileMonitoringMaxSizeMB = maxSize;
            }
        }

        // 언어 저장
        var oldLang = settings.Language;
        settings.Language = LanguageComboBox.SelectedIndex == 1 ? "ko" : "en";
        bool langChanged = oldLang != settings.Language;
        
        settings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? true;
        
        // Windows 시작 프로그램 등록/해제 (변경된 경우에만)
        bool wantStartup = StartWithWindowsCheckBox.IsChecked ?? false;
        bool currentStartup = settings.StartWithWindows;
        
        if (wantStartup != currentStartup)
        {
            bool success = SetStartupWithWindows(wantStartup, showMessage: true);
            if (success)
            {
                settings.StartWithWindows = wantStartup;
            }
            else if (wantStartup)
            {
                // 실패 시 체크박스 원상복구
                StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
            }
        }

        _settingsService.Save();

        if (langChanged)
        {
            Loc.Language = settings.Language;
            System.Windows.MessageBox.Show(Loc.MsgRestartRequired, "AutoPad", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        Close();
    }

    private bool SetStartupWithWindows(bool enable, bool showMessage = false)
    {
        if (IsRunningAsMsix())
            return SetStartupWithWindowsMsix(enable, showMessage);
        else
            return SetStartupWithWindowsRegistry(enable, showMessage);
    }

    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    private static bool IsRunningAsMsix()
    {
        try
        {
            int length = 0;
            int result = GetCurrentPackageFullName(ref length, null);
            return result != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    private bool SetStartupWithWindowsMsix(bool enable, bool showMessage)
    {
        try
        {
            var task = global::Windows.ApplicationModel.StartupTask.GetAsync("AutoPadStartup").GetAwaiter().GetResult();

            if (enable)
            {
                var result = task.RequestEnableAsync().GetAwaiter().GetResult();
                if (result == global::Windows.ApplicationModel.StartupTaskState.Enabled)
                {
                    if (showMessage)
                        System.Windows.MessageBox.Show(Loc.MsgStartupRegistered, Loc.MsgSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                else if (result == global::Windows.ApplicationModel.StartupTaskState.DisabledByUser)
                {
                    System.Windows.MessageBox.Show(Loc.MsgStartupDisabledByUser, Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return false;
            }
            else
            {
                task.Disable();
                if (showMessage)
                    System.Windows.MessageBox.Show(Loc.MsgStartupUnregistered, Loc.MsgSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(Loc.MsgStartupError(ex.Message), Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool SetStartupWithWindowsRegistry(bool enable, bool showMessage = false)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null)
            {
                System.Windows.MessageBox.Show(Loc.MsgRegistryFailed, Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (enable)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    System.Windows.MessageBox.Show(Loc.MsgExePathFailed, Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // dotnet run으로 실행 중인 경우 dll 경로가 올 수 있음
                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    // dll 대신 dotnet 명령어로 실행하도록 설정
                    key.SetValue(AppName, $"dotnet \"{exePath}\"");
                }
                else
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
                
                // 등록 확인
                var registered = key.GetValue(AppName);
                if (registered != null)
                {
                    if (showMessage)
                        System.Windows.MessageBox.Show(Loc.MsgStartupRegistered, Loc.MsgSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                if (showMessage)
                    System.Windows.MessageBox.Show(Loc.MsgStartupUnregistered, Loc.MsgSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(Loc.MsgStartupError(ex.Message), Loc.MsgError, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void PositionComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is WpfComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            comboBox.IsDropDownOpen = true;
            e.Handled = true;
        }
    }
}
