namespace AutoPad.Models;

/// <summary>
/// 앱 설정 모델
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 토스트 위치 (Bottom, Top, BottomLeft, BottomRight, TopLeft, TopRight)
    /// </summary>
    public ToastPosition ToastPosition { get; set; } = ToastPosition.BottomRight;

    /// <summary>
    /// 토스트 표시 시간 (초)
    /// </summary>
    public int ToastDurationSeconds { get; set; } = 5;

    /// <summary>
    /// 클립보드 모니터링 활성화 여부
    /// </summary>
    public bool IsMonitoringEnabled { get; set; } = true;

    /// <summary>
    /// 시작 시 자동 실행
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// 시작 시 최소화
    /// </summary>
    public bool StartMinimized { get; set; } = true;

    /// <summary>
    /// 다크 모드 사용
    /// </summary>
    public bool IsDarkMode { get; set; } = true;

    /// <summary>
    /// 언어 설정 ("en" 또는 "ko")
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// 파일 복사 모니터링 활성화
    /// </summary>
    public bool IsFileMonitoringEnabled { get; set; } = true;

    /// <summary>
    /// 이미지 복사 모니터링 활성화
    /// </summary>
    public bool IsImageMonitoringEnabled { get; set; } = true;

    /// <summary>
    /// 파일 복사 모니터링 크기 제한 (MB)
    /// </summary>
    public int FileMonitoringMaxSizeMB { get; set; } = 10;

    /// <summary>
    /// 컴팩트 모드 (토스트에 버튼만 표시)
    /// </summary>
    public bool IsCompactMode { get; set; } = false;

    /// <summary>
    /// 토스트 투명도 (10~100%)
    /// </summary>
    public int ToastOpacityPercent { get; set; } = 100;

    /// <summary>
    /// 클립보드 히스토리 활성화
    /// </summary>
    public bool IsHistoryEnabled { get; set; } = true;

    /// <summary>
    /// 히스토리 최대 저장 개수
    /// </summary>
    public int HistoryMaxItems { get; set; } = 50;

    /// <summary>
    /// 매크로 목록
    /// </summary>
    public List<MacroItem> Macros { get; set; } = new();

    /// <summary>
    /// 맞춤법 검사 활성화
    /// </summary>
    public bool IsSpellCheckEnabled { get; set; } = false;
}

public enum ToastPosition
{
    Bottom,
    Top,
    BottomLeft,
    BottomRight,
    TopLeft,
    TopRight
}
