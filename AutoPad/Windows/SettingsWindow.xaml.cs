using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using AutoPad.Models;
using AutoPad.Services;
using Microsoft.Win32;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;namespace AutoPad.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private ToastWindow? _previewToast;
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
        
        // 탭
        TabGeneral.Content = Loc.SettingsTabGeneral;
        TabNotification.Content = Loc.SettingsTabNotification;
        TabHistory.Content = Loc.SettingsTabHistory;
        TabMacro.Content = Loc.SettingsTabMacro;
        
        // 일반 탭
        LanguageLabelText.Text = Loc.SettingsLanguage;
        MonitoringCheckBox.Content = Loc.SettingsEnableMonitoring;
        ImageMonitoringCheckBox.Content = Loc.SettingsEnableImageMonitoring;
        FileMonitoringCheckBox.Content = Loc.SettingsEnableFileMonitoring;
        FileSizeLimitLabel.Text = Loc.SettingsFileSizeLimit;
        StartWithWindowsCheckBox.Content = Loc.SettingsAutoStart;
        StartMinimizedCheckBox.Content = Loc.SettingsStartMinimized;
        SpellCheckCheckBox.Content = Loc.SpellCheckLabel;
        SpellCheckTooltipText.Text = Loc.SpellCheckTooltip;
        
        // 알림 탭
        PositionLabelText.Text = Loc.SettingsToastPosition;
        DurationLabelText.Text = Loc.SettingsDuration;
        CompactModeCheckBox.Content = Loc.SettingsCompactMode;
        OpacityLabelText.Text = Loc.SettingsToastOpacity;
        
        // 히스토리 탭
        HistoryCheckBox.Content = Loc.SettingsEnableHistory;
        HistorySizeLabel.Text = Loc.SettingsHistorySize;
        
        SettingsSaveButton.Content = Loc.SettingsBtnSave;

        // 매크로 탭
        MacroDescLabel.Text = Loc.MacroDescription;
        MacroPresetButtonText.Text = Loc.MacroPresetBtn;

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"v{version!.Major}.{version.Minor}.{version.Build}.{version.Revision}";

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
        ImageMonitoringCheckBox.IsChecked = settings.IsImageMonitoringEnabled;
        FileMonitoringCheckBox.IsChecked = settings.IsFileMonitoringEnabled;
        UpdateMonitoringChildren();
        
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
        SpellCheckCheckBox.IsChecked = settings.IsSpellCheckEnabled;
        CompactModeCheckBox.IsChecked = settings.IsCompactMode;
        OpacitySlider.Value = settings.ToastOpacityPercent;
        OpacityValueText.Text = $"{settings.ToastOpacityPercent}%";
        HistoryCheckBox.IsChecked = settings.IsHistoryEnabled;

        // 히스토리 개수 선택
        foreach (WpfComboBoxItem item in HistorySizeComboBox.Items)
        {
            if (item.Tag?.ToString() == settings.HistoryMaxItems.ToString())
            {
                HistorySizeComboBox.SelectedItem = item;
                break;
            }
        }
        if (HistorySizeComboBox.SelectedItem == null && HistorySizeComboBox.Items.Count > 0)
        {
            HistorySizeComboBox.SelectedIndex = 1; // 기본값 50
        }

        // 언어 선택
        LanguageComboBox.SelectedIndex = settings.Language == "ko" ? 1 : 0;

        // 매크로 목록 로드
        RefreshMacroList();
    }

    private void MonitoringCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateMonitoringChildren();
    }

    private void UpdateMonitoringChildren()
    {
        bool enabled = MonitoringCheckBox.IsChecked ?? false;
        ImageMonitoringCheckBox.IsEnabled = enabled;
        FileMonitoringCheckBox.IsEnabled = enabled;
        FileSizeComboBox.IsEnabled = enabled;
        FileSizeLimitLabel.IsEnabled = enabled;
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelGeneral == null) return; // InitializeComponent not done yet
        PanelGeneral.Visibility = TabGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelNotification.Visibility = TabNotification.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelHistory.Visibility = TabHistory.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelMacro.Visibility = TabMacro.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        if (TabNotification.IsChecked == true)
            ShowPreviewToast();
        else
            ClosePreviewToast();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText != null)
            OpacityValueText.Text = $"{(int)e.NewValue}%";
        _previewToast?.UpdatePreviewOpacity(e.NewValue / 100.0);
    }

    private void PositionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TabNotification?.IsChecked == true)
            ShowPreviewToast();
    }

    private void NotificationSettingChanged(object sender, RoutedEventArgs e)
    {
        if (TabNotification?.IsChecked == true)
            ShowPreviewToast();
    }

    private void ShowPreviewToast()
    {
        ClosePreviewToast();

        var position = ToastPosition.BottomRight;
        if (PositionComboBox.SelectedItem is WpfComboBoxItem posItem
            && Enum.TryParse<ToastPosition>(posItem.Tag?.ToString(), out var pos))
        {
            position = pos;
        }

        var isCompact = CompactModeCheckBox.IsChecked ?? false;
        var opacityPercent = (int)OpacitySlider.Value;

        _previewToast = new ToastWindow(position, isCompact, opacityPercent);
        _previewToast.Show();
    }

    private void ClosePreviewToast()
    {
        if (_previewToast != null)
        {
            _previewToast.Close();
            _previewToast = null;
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        ClosePreviewToast();
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
        settings.IsImageMonitoringEnabled = ImageMonitoringCheckBox.IsChecked ?? true;
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
        settings.IsSpellCheckEnabled = SpellCheckCheckBox.IsChecked ?? false;
        settings.IsCompactMode = CompactModeCheckBox.IsChecked ?? false;
        settings.ToastOpacityPercent = (int)OpacitySlider.Value;
        settings.IsHistoryEnabled = HistoryCheckBox.IsChecked ?? true;

        // 히스토리 개수 저장
        if (HistorySizeComboBox.SelectedItem is WpfComboBoxItem histItem)
        {
            if (int.TryParse(histItem.Tag?.ToString(), out var maxItems))
            {
                settings.HistoryMaxItems = maxItems;
            }
        }
        
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

    // ── 매크로 관리 ──

    private void RefreshMacroList()
    {
        MacroListBox.Items.Clear();
        foreach (var macro in _settingsService.Settings.Macros)
        {
            MacroListBox.Items.Add(macro.Name);
        }
    }

    private void MacroListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var hasSelection = MacroListBox.SelectedIndex >= 0;
        var index = MacroListBox.SelectedIndex;
        var count = _settingsService.Settings.Macros.Count;
        MacroEditButton.IsEnabled = hasSelection;
        MacroDeleteButton.IsEnabled = hasSelection;
        MacroMoveUpButton.IsEnabled = hasSelection && index > 0;
        MacroMoveDownButton.IsEnabled = hasSelection && index < count - 1;
    }

    private void MacroListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete && MacroListBox.SelectedIndex >= 0)
        {
            MacroDeleteButton_Click(sender, e);
            e.Handled = true;
        }
    }

    private void MacroAddButton_Click(object sender, RoutedEventArgs e)
    {
        var macro = new MacroItem();
        var editor = new MacroEditorWindow(macro, _settingsService.Settings.IsDarkMode)
        {
            Owner = this
        };
        editor.ShowDialog();

        if (editor.IsSaved)
        {
            _settingsService.Settings.Macros.Add(macro);
            _settingsService.Save();
            RefreshMacroList();
        }
    }

    private void MacroEditButton_Click(object sender, RoutedEventArgs e)
    {
        var index = MacroListBox.SelectedIndex;
        if (index < 0 || index >= _settingsService.Settings.Macros.Count) return;

        var macro = _settingsService.Settings.Macros[index];
        var editCopy = new MacroItem
        {
            Id = macro.Id,
            Name = macro.Name,
            Script = macro.Script
        };

        var editor = new MacroEditorWindow(editCopy, _settingsService.Settings.IsDarkMode)
        {
            Owner = this
        };
        editor.ShowDialog();

        if (editor.IsSaved)
        {
            macro.Name = editCopy.Name;
            macro.Script = editCopy.Script;
            _settingsService.Save();
            RefreshMacroList();
        }
    }

    private void MacroDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var index = MacroListBox.SelectedIndex;
        if (index < 0 || index >= _settingsService.Settings.Macros.Count) return;

        var name = _settingsService.Settings.Macros[index].Name;
        var result = System.Windows.MessageBox.Show(
            Loc.MacroDeleteConfirm(name),
            "AutoPad",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _settingsService.Settings.Macros.RemoveAt(index);
            _settingsService.Save();
            RefreshMacroList();
        }
    }

    private void MacroMoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        var index = MacroListBox.SelectedIndex;
        if (index <= 0) return;

        var macros = _settingsService.Settings.Macros;
        (macros[index - 1], macros[index]) = (macros[index], macros[index - 1]);
        _settingsService.Save();
        RefreshMacroList();
        MacroListBox.SelectedIndex = index - 1;
    }

    private void MacroMoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        var index = MacroListBox.SelectedIndex;
        var macros = _settingsService.Settings.Macros;
        if (index < 0 || index >= macros.Count - 1) return;

        (macros[index], macros[index + 1]) = (macros[index + 1], macros[index]);
        _settingsService.Save();
        RefreshMacroList();
        MacroListBox.SelectedIndex = index + 1;
    }

    private void MacroPresetButton_Click(object sender, RoutedEventArgs e)
    {
        var existingNames = _settingsService.Settings.Macros.Select(m => m.Name).ToList();
        var presetWindow = new MacroPresetWindow(existingNames, _settingsService.Settings.IsDarkMode)
        {
            Owner = this
        };
        presetWindow.ShowDialog();

        if (presetWindow.SelectedMacros.Count > 0)
        {
            foreach (var macro in presetWindow.SelectedMacros)
            {
                _settingsService.Settings.Macros.Add(macro);
            }
            _settingsService.Save();
            RefreshMacroList();
        }
    }
}
