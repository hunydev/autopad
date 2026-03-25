using System.Text.RegularExpressions;
using System.Windows;
using AutoPad.Services;

namespace AutoPad.Windows;

public partial class HtmlViewerWindow : Window
{
    public HtmlViewerWindow(string cfHtml, bool isDarkMode = true)
    {
        InitializeComponent();

        Title = Loc.HtmlViewerTitle;
        HtmlLabel.Text = Loc.HtmlViewerLabel;

        SourceInitialized += (s, e) => ThemeHelper.ApplyDarkTitleBar(this, isDarkMode);

        var html = ExtractHtmlFromCfHtml(cfHtml);
        HtmlSourceTextBox.Text = html ?? cfHtml;
    }

    /// <summary>
    /// CF_HTML 형식 문자열에서 실제 HTML 컨텐츠를 추출
    /// </summary>
    private static string? ExtractHtmlFromCfHtml(string cfHtml)
    {
        // CF_HTML 헤더에서 StartHTML/EndHTML 바이트 오프셋 파싱
        var startMatch = Regex.Match(cfHtml, @"StartHTML:(\d+)");
        var endMatch = Regex.Match(cfHtml, @"EndHTML:(\d+)");

        if (startMatch.Success && endMatch.Success)
        {
            int startOffset = int.Parse(startMatch.Groups[1].Value);
            int endOffset = int.Parse(endMatch.Groups[1].Value);

            // CF_HTML은 UTF-8 바이트 오프셋 사용 — 바이트 기반으로 추출
            var bytes = System.Text.Encoding.UTF8.GetBytes(cfHtml);
            if (startOffset >= 0 && endOffset > startOffset && endOffset <= bytes.Length)
            {
                return System.Text.Encoding.UTF8.GetString(bytes, startOffset, endOffset - startOffset);
            }
        }

        // 오프셋 파싱 실패 시 HTML 태그 기반 폴백
        int idx = cfHtml.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = cfHtml.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) return cfHtml[idx..];

        return null;
    }
}
