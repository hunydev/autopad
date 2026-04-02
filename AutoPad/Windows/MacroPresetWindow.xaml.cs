using System.Windows;
using System.Windows.Controls;
using AutoPad.Models;
using AutoPad.Services;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace AutoPad.Windows;

public partial class MacroPresetWindow : Window
{
    private readonly List<string> _existingNames;
    private readonly List<(WpfCheckBox cb, MacroItem macro)> _items = new();

    public List<MacroItem> SelectedMacros { get; } = new();

    public MacroPresetWindow(List<string> existingMacroNames, bool isDarkMode = true)
    {
        InitializeComponent();
        _existingNames = existingMacroNames;

        Icon = IconHelper.CreateAppIconImageSource(32);
        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, isDarkMode);

        ApplyLocalization();
        BuildPresetList();
        UpdateSelectionCount();
    }

    private void ApplyLocalization()
    {
        Title = Loc.MacroPresetTitle;
        PresetHeaderText.Text = Loc.MacroPresetHeader;
        PresetDescText.Text = Loc.MacroPresetDesc;
        CancelBtn.Content = Loc.MacroEditorCancel;
        AddBtn.Content = Loc.MacroPresetAdd;
    }

    private void BuildPresetList()
    {
        var presets = MacroService.GetPresets();
        foreach (var preset in presets)
        {
            bool alreadyExists = _existingNames.Contains(preset.Name);

            var cb = new WpfCheckBox
            {
                Content = preset.Name,
                Style = (Style)FindResource("PresetCheckBox"),
                IsChecked = false,
                IsEnabled = !alreadyExists,
                Opacity = alreadyExists ? 0.5 : 1.0,
                ToolTip = alreadyExists ? Loc.MacroPresetAlreadyAdded : null
            };
            cb.Checked += (s, e) => UpdateSelectionCount();
            cb.Unchecked += (s, e) => UpdateSelectionCount();

            _items.Add((cb, preset));
            PresetPanel.Children.Add(cb);
        }
    }

    private void UpdateSelectionCount()
    {
        int count = 0;
        foreach (var (cb, _) in _items)
        {
            if (cb.IsChecked == true) count++;
        }
        SelectionCountText.Text = Loc.MacroPresetSelected(count);
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (cb, macro) in _items)
        {
            if (cb.IsChecked == true)
            {
                // 새 ID를 생성하여 독립 복사
                SelectedMacros.Add(new MacroItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = macro.Name,
                    Script = macro.Script
                });
            }
        }
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
