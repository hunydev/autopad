using System.Windows;
using AutoPad.Models;
using AutoPad.Services;

namespace AutoPad.Windows;

public partial class MacroEditorWindow : Window
{
    private readonly MacroItem _macro;

    public bool IsSaved { get; private set; }

    public MacroEditorWindow(MacroItem macro, bool isDarkMode = true)
    {
        InitializeComponent();
        _macro = macro;

        Icon = IconHelper.CreateAppIconImageSource(32);
        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, isDarkMode);

        NameTextBox.Text = macro.Name;
        ScriptTextBox.Text = macro.Script;

        ApplyLocalization();

        Loaded += (_, _) => { NameTextBox.Focus(); NameTextBox.SelectAll(); };
    }

    private void ApplyLocalization()
    {
        Title = Loc.MacroEditorTitle;
        NameLabel.Text = Loc.MacroEditorName;
        ScriptLabel.Text = Loc.MacroEditorScript;
        TestInputLabel.Text = Loc.MacroEditorTestInput;
        TestOutputLabel.Text = Loc.MacroEditorTestOutput;
        TestRunButton.ToolTip = Loc.MacroEditorRunTest;
        CancelButton.Content = Loc.MacroEditorCancel;
        SaveMacroButton.Content = Loc.MacroEditorSave;
    }

    private void TestRunButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        TestOutputTextBox.Text = "";
        TestOutputTextBox.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x4E, 0xC9, 0xB0));

        var script = ScriptTextBox.Text;
        var input = TestInputTextBox.Text;

        var (success, output, error) = MacroService.TestMacro(script, input);

        if (success)
        {
            TestOutputTextBox.Text = output;
        }
        else
        {
            TestOutputTextBox.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xF4, 0x47, 0x47));
            TestOutputTextBox.Text = "";
            ErrorText.Text = error ?? "Unknown error";
        }
    }

    private void SaveMacroButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ErrorText.Text = Loc.MacroEditorNameRequired;
            return;
        }

        var script = ScriptTextBox.Text;

        // Validate script by running a test
        var (success, _, error) = MacroService.TestMacro(script, "test");
        if (!success)
        {
            ErrorText.Text = Loc.MacroEditorScriptError(error ?? "Unknown error");
            return;
        }

        _macro.Name = name;
        _macro.Script = script;
        IsSaved = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
