namespace AutoPad.Services;

/// <summary>
/// 앱 전체 다국어 지원 서비스
/// </summary>
public static class Loc
{
    private static string _lang = "en";

    public static string Language
    {
        get => _lang;
        set => _lang = value == "ko" ? "ko" : "en";
    }

    private static string Get(string en, string ko) => _lang == "ko" ? ko : en;

    // ── App ──
    public static string AppAlreadyRunning => Get("AutoPad is already running.", "AutoPad가 이미 실행 중입니다.");
    public static string TrayTooltip => Get("AutoPad - Clipboard Editor", "AutoPad - 클립보드 편집 도구");
    public static string TrayMonitoring => Get("Clipboard Monitoring", "클립보드 모니터링");
    public static string TraySettings => Get("Settings", "설정");
    public static string TrayExit => Get("Exit", "종료");

    // ── Toast ──
    public static string TextCopied(int runeCount, string sizeText, int lineCount)
        => Get($"Text copied ({runeCount} chars, {lineCount} lines, {sizeText})", $"텍스트 복사됨 ({runeCount}글자, {lineCount}줄, {sizeText})");
    public static string ImageCopied => Get("Image copied to clipboard", "이미지가 클립보드에 복사되었습니다");
    public static string FileCopied => Get("File copied to clipboard", "파일이 클립보드에 복사되었습니다");
    public static string BtnSave => Get("Save", "저장");
    public static string BtnEdit => Get("Edit", "편집");
    public static string BtnHtmlSource => Get("HTML Source", "HTML 소스");
    public static string BtnFileEdit => Get("File Edit", "파일 편집");
    public static string BtnFileOpen => Get("Open File", "파일 열기");
    public static string BtnFolderOpen => Get("Open Folder", "폴더 열기");
    public static string BtnUrlOpen => Get("Open URL", "URL 열기");
    public static string BtnCopy => Get("Copy", "복사");
    public static string ToastPreviewText => Get("Toast Preview", "알림 미리보기");
    public static string TextFileFilter => Get("Text Files (*.txt)|*.txt|All Files (*.*)|*.*", "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*");
    public static string ImageFileFilter => Get("PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|BMP Image (*.bmp)|*.bmp", "PNG 이미지 (*.png)|*.png|JPEG 이미지 (*.jpg)|*.jpg|BMP 이미지 (*.bmp)|*.bmp");
    public static string SaveFailed(string msg) => Get($"Save failed: {msg}", $"저장 실패: {msg}");

    // ── Edit Window ──
    public static string EditTitle(int runeCount, string sizeText, int lineCount)
        => Get($"AutoPad - Edit ({runeCount} chars, {lineCount} lines, {sizeText})", $"AutoPad - 편집 ({runeCount}글자, {lineCount}줄, {sizeText})");
    public static string EditTitleImage(int w, int h)
        => Get($"AutoPad - Image ({w} x {h})", $"AutoPad - 이미지 ({w} x {h})");
    public static string EditSubtitle => Get("Edit content and copy", "내용을 편집하고 복사하세요");
    public static string LabelEncoding => Get("Encoding:", "인코딩:");
    public static string ToolPen => Get("Pen", "펜");
    public static string ToolEraser => Get("Eraser", "지우개");
    public static string ToolSelect => Get("Select Region", "영역 선택");
    public static string LabelThickness => Get("Size:", "굵기:");
    public static string ToolEraseStroke => Get("Stroke Erase", "획 지우기");
    public static string ToolEraseStrokeTip => Get("Erase entire pen stroke", "펜 획 전체를 지움");
    public static string ToolErasePoint => Get("Point Erase", "부분 지우기");
    public static string ToolErasePointTip => Get("Erase only where eraser touches", "지우개가 닿는 부분만 지움");
    public static string LabelSize => Get("Size:", "크기:");
    public static string ToolMosaic => Get("Mosaic", "모자이크");
    public static string ToolMosaicTip => Get("Mosaic selected region", "선택 영역 모자이크");
    public static string ToolFill => Get("Fill", "채우기");
    public static string ToolFillTip => Get("Fill selected region with color", "선택 영역 단색 채우기 (색상 선택)");
    public static string ToolErase => Get("Erase", "없애기");
    public static string ToolEraseTip => Get("Erase selected region (transparent)", "선택 영역 없애기 (투명)");
    public static string ToolUndo => Get("Undo (Ctrl+Z)", "실행취소 (Ctrl+Z)");
    public static string ToolClearAll => Get("Clear all drawings", "그린 내용 모두 지우기");
    public static string ToolFitToggle => Get("Fit to window / Original size", "창에 맞추기 / 원본 크기");
    public static string FitOriginal => Get("Original", "원본");
    public static string FitToWindow => Get("Fit", "맞추기");
    public static string BtnSaveFile => Get("Save", "저장");
    public static string BtnCopySelection => Get("Copy Selection", "선택 영역 복사");
    public static string BtnCopyClose => Get("Copy & Close", "복사 후 닫기");
    public static string StatusFilled => Get("Filled selection", "선택 영역을 채웠습니다");
    public static string StatusMosaic => Get("Applied mosaic", "모자이크를 적용했습니다");
    public static string StatusErased => Get("Removed selection", "선택 영역을 제거했습니다");
    public static string StatusUndone => Get("Restored", "되돌렸습니다");
    public static string StatusCleared => Get("Cleared all", "모두 지웠습니다");
    public static string StatusNoSelection => Get("No text selected", "선택된 텍스트가 없습니다");
    public static string StatusSelCopied(int runeCount, string sizeText)
        => Get($"Selection copied ({runeCount} chars, {sizeText})", $"선택 영역 복사됨 ({runeCount}글자, {sizeText})");
    public static string StatusNothingToCopy => Get("Nothing to copy", "복사할 내용이 없습니다");
    public static string StatusSaved => Get("Saved!", "저장되었습니다!");
    public static string StatusSelCopiedImage(int w, int h)
        => Get($"Selection copied ({w}x{h} px)", $"선택 영역 복사됨 ({w}x{h} px)");
    public static string ImageLoadFailed(string msg) => Get($"Image load failed: {msg}", $"이미지 로드 실패: {msg}");
    public static string FileNotFound => Get("File does not exist.", "파일이 존재하지 않습니다.");
    public static string FileReadFailed(string msg) => Get($"Cannot read file: {msg}", $"파일을 읽을 수 없습니다: {msg}");

    // ── Text Tools ──
    public static string BtnRemoveLines => Get("Remove Lines", "라인 제거");
    public static string BtnRemoveBlankLines => Get("Blank Lines", "공백 라인 제거");
    public static string BtnRemoveAllLines => Get("All Lines", "모든 라인 제거");
    public static string BtnReplaceWhitespace => Get("Remove Special Whitespace", "특수공백 제거");
    public static string BtnTrim => Get("TRIM", "앞뒤 공백 제거");
    public static string BtnMaskNumbers => Get("Mask Numbers", "숫자 마스킹");
    public static string BtnUpperCase => Get("UPPER", "대문자");
    public static string BtnLowerCase => Get("lower", "소문자");
    public static string StatusBlankLinesRemoved => Get("Blank lines removed", "공백 라인을 제거했습니다");
    public static string StatusAllLinesRemoved => Get("All line breaks removed", "모든 줄바꾼을 제거했습니다");
    public static string StatusNoWhitespaceFound => Get("No special whitespace found", "특수 공백이 없습니다");
    public static string MsgWhitespaceReplaced(int total, string detail)
        => Get($"Removed {total} non-ASCII whitespace(s):\n{detail}", $"비ASCII 공백 {total}개 제거:\n{detail}");
    public static string StatusTrimmed => Get("Lines trimmed", "라인 앞뒤 공백을 제거했습니다");
    public static string StatusNoNumbersFound => Get("No numbers found", "숫자가 없습니다");
    public static string MsgNumbersMasked(int count)
        => Get($"{count} digit(s) masked with *", $"숫자 {count}개를 *로 마스킹했습니다");
    public static string StatusUpperCased => Get("Converted to UPPERCASE", "대문자로 변환했습니다");
    public static string StatusLowerCased => Get("Converted to lowercase", "소문자로 변환했습니다");
    public static string SelectionInfoFull(int runeCount, string sizeText, int lineCount)
        => Get($"{runeCount} chars | {lineCount} lines | {sizeText}", $"{runeCount}글자 | {lineCount}줄 | {sizeText}");
    public static string SelectionInfoSelected(int runeCount, string sizeText, int lineCount)
        => Get($"Selected: {runeCount} chars | {lineCount} lines | {sizeText}", $"선택: {runeCount}글자 | {lineCount}줄 | {sizeText}");

    // ── HTML Viewer ──
    public static string HtmlViewerTitle => Get("AutoPad - HTML Source", "AutoPad - HTML 소스");
    public static string HtmlViewerLabel => Get("HTML Source (Read-only)", "HTML 소스 (읽기 전용)");

    // ── Settings ──
    public static string SettingsTitle => Get("AutoPad - Settings", "AutoPad - 설정");
    public static string SettingsHeader => Get("Settings", "설정");
    public static string SettingsTabGeneral => Get("General", "일반");
    public static string SettingsTabNotification => Get("Notification", "알림");
    public static string SettingsTabHistory => Get("History", "히스토리");
    public static string SettingsLanguage => Get("Language", "언어");
    public static string LangEnglish => Get("English", "English");
    public static string LangKorean => Get("한국어", "한국어");
    public static string SettingsToastPosition => Get("Toast Position", "토스트 알림 위치");
    public static string PosBottomCenter => Get("Bottom Center", "하단 중앙");
    public static string PosTopCenter => Get("Top Center", "상단 중앙");
    public static string PosBottomLeft => Get("Bottom Left", "하단 왼쪽");
    public static string PosBottomRight => Get("Bottom Right", "하단 오른쪽");
    public static string PosTopLeft => Get("Top Left", "상단 왼쪽");
    public static string PosTopRight => Get("Top Right", "상단 오른쪽");
    public static string SettingsDuration => Get("Notification Duration (seconds)", "알림 표시 시간 (초)");
    public static string Duration(int sec) => Get($"{sec} sec", $"{sec}초");
    public static string SettingsEnableMonitoring => Get("Enable Clipboard Monitoring", "클립보드 모니터링 활성화");
    public static string SettingsEnableFileMonitoring => Get("Enable File Copy Monitoring", "파일 복사 모니터링 활성화");
    public static string SettingsEnableImageMonitoring => Get("Enable Image Copy Monitoring", "이미지 복사 모니터링 활성화");
    public static string SettingsFileSizeLimit => Get("File Size Limit:", "파일 크기 제한:");
    public static string SettingsAutoStart => Get("Auto-run on Windows Startup", "Windows 시작 시 자동 실행");
    public static string SettingsStartMinimized => Get("Start Minimized to Tray", "시작 시 최소화 (트레이)");
    public static string SettingsCompactMode => Get("Compact Toast (buttons only)", "컴팩트 알림 (버튼만 표시)");
    public static string SettingsToastOpacity => Get("Toast Opacity", "알림 투명도");
    public static string SettingsBtnSave => Get("Save", "저장");

    // ── Settings Messages ──
    public static string MsgError => Get("Error", "오류");
    public static string MsgSuccess => Get("Success", "성공");
    public static string MsgRegistryFailed => Get("Cannot access registry.", "레지스트리에 접근할 수 없습니다.");
    public static string MsgExePathFailed => Get("Cannot find executable path.", "실행 파일 경로를 찾을 수 없습니다.");
    public static string MsgStartupRegistered => Get("Auto-run on Windows startup has been registered.", "Windows 시작 시 자동 실행이 등록되었습니다.");
    public static string MsgStartupUnregistered => Get("Auto-run on Windows startup has been unregistered.", "Windows 시작 시 자동 실행이 해제되었습니다.");
    public static string MsgStartupDisabledByUser => Get("Startup has been disabled by the user in Windows Settings.\nPlease enable it in Settings > Apps > Startup.", "Windows 설정에서 시작 프로그램이 비활성화되었습니다.\n설정 > 앱 > 시작에서 활성화해 주세요.");
    public static string MsgStartupError(string msg) => Get($"Error during startup registration:\n{msg}", $"시작 프로그램 등록 중 오류가 발생했습니다.\n{msg}");
    public static string MsgRestartRequired => Get("Language change will be applied after restarting the app.", "언어 변경은 앱을 다시 시작한 후 적용됩니다.");

    // ── History ──
    public static string TrayHistory => Get("Clipboard History", "클립보드 히스토리");
    public static string HistoryTitle => Get("AutoPad - Clipboard History", "AutoPad - 클립보드 히스토리");
    public static string HistoryHeader => Get("Clipboard History", "클립보드 히스토리");
    public static string HistorySearch => Get("Search...", "검색...");
    public static string HistoryClearAll => Get("Clear All", "모두 삭제");
    public static string HistoryClearConfirm => Get("Clear all clipboard history?", "클립보드 히스토리를 모두 삭제하시겠습니까?");
    public static string HistoryCount(int count) => Get($"{count} item(s)", $"{count}개 항목");
    public static string SettingsEnableHistory => Get("Enable Clipboard History", "클립보드 히스토리 활성화");
    public static string SettingsHistorySize => Get("History Size:", "히스토리 개수:");

    // ── Size formatting ──
    public static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
