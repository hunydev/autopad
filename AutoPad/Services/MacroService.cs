using System.Text.RegularExpressions;
using AutoPad.Models;
using Jint;
using Jint.Runtime;

namespace AutoPad.Services;

/// <summary>
/// 안전한 JS 래퍼 — 매크로에서 host.regexReplace() 사용 가능
/// </summary>
public sealed class SafeHost
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    public string RegexReplace(string text, string pattern, string replacement)
    {
        return Regex.Replace(text, pattern, replacement, RegexOptions.None, RegexTimeout);
    }

    public string RegexMatch(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.None, RegexTimeout);
        return match.Success ? match.Value : "";
    }

    public string[] RegexMatches(string text, string pattern)
    {
        var matches = Regex.Matches(text, pattern, RegexOptions.None, RegexTimeout);
        var results = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
            results[i] = matches[i].Value;
        return results;
    }
}

/// <summary>
/// Jint 기반 매크로 실행 서비스 — 완전 샌드박스
/// </summary>
public static class MacroService
{
    /// <summary>
    /// 기본 프리셋 매크로 목록
    /// </summary>
    public static List<MacroItem> GetPresets() => new()
    {
        new MacroItem
        {
            Name = Loc.PresetPhoneMask,
            Script =
                """
                function transform(input) {
                  return input.replace(
                    /(\+?\d{1,3}[\s.-]?)?(\(?\d{1,4}\)?[\s.-]?)(\d[\d\s.\-]{2,7}\d)([\s.-]?\d{3,4})/g,
                    function(m, cc, area, middle, last) {
                      cc = cc || '';
                      return cc + area + middle.replace(/\d/g, '*') + last;
                    }
                  );
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetEmailMask,
            Script =
                """
                function transform(input) {
                  return input.replace(
                    /([a-zA-Z0-9._%+\-]+)(@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,})/g,
                    function(m, local, domain) {
                      if (local.length <= 1) return local + '***' + domain;
                      return local[0] + '*'.repeat(local.length - 1) + domain;
                    }
                  );
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetCreditCardMask,
            Script =
                """
                function transform(input) {
                  return input.replace(
                    /\b(\d{4})[\s.-]?(\d{4})[\s.-]?(\d{4})[\s.-]?(\d{4})\b/g,
                    function(m, g1, g2, g3, g4) {
                      return g1 + '-****-****-' + g4;
                    }
                  );
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetIpAddressMask,
            Script =
                """
                function transform(input) {
                  return input.replace(
                    /\b(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})\b/g,
                    function(m, a, b, c, d) {
                      return a + '.' + b + '.***.' + '***';
                    }
                  );
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetJsonFormat,
            Script =
                """
                function transform(input) {
                  var obj = JSON.parse(input);
                  return JSON.stringify(obj, null, 2);
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetJsonMinify,
            Script =
                """
                function transform(input) {
                  var obj = JSON.parse(input);
                  return JSON.stringify(obj);
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetRemoveDuplicateLines,
            Script =
                """
                function transform(input) {
                  var lines = input.split('\n');
                  var seen = {};
                  var result = [];
                  for (var i = 0; i < lines.length; i++) {
                    var key = lines[i].replace(/\r$/, '');
                    if (!seen[key]) {
                      seen[key] = true;
                      result.push(lines[i]);
                    }
                  }
                  return result.join('\n');
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetSortLines,
            Script =
                """
                function transform(input) {
                  var lines = input.split('\n');
                  lines.sort(function(a, b) {
                    return a.replace(/\r$/, '').localeCompare(b.replace(/\r$/, ''));
                  });
                  return lines.join('\n');
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetCamelToSnake,
            Script =
                """
                function transform(input) {
                  return input.replace(
                    /[A-Z]/g,
                    function(ch, idx) {
                      return (idx > 0 ? '_' : '') + ch.toLowerCase();
                    }
                  );
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetSnakeToCamel,
            Script =
                """
                function transform(input) {
                  return input.replace(
                    /_([a-z])/g,
                    function(m, ch) {
                      return ch.toUpperCase();
                    }
                  );
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetMarkdownTable,
            Script =
                """
                function transform(input) {
                  var lines = input.split('\n').filter(function(l) {
                    return l.replace(/\r/, '').trim().length > 0;
                  });
                  if (lines.length === 0) return input;
                  var sep = lines[0].indexOf('\t') >= 0 ? '\t' : ',';
                  var rows = lines.map(function(l) {
                    return l.replace(/\r$/, '').split(sep).map(function(c) { return c.trim(); });
                  });
                  var cols = 0;
                  for (var i = 0; i < rows.length; i++) {
                    if (rows[i].length > cols) cols = rows[i].length;
                  }
                  var header = '| ' + rows[0].join(' | ') + ' |';
                  var divider = '|' + ' --- |'.repeat(cols);
                  var body = rows.slice(1).map(function(r) {
                    while (r.length < cols) r.push('');
                    return '| ' + r.join(' | ') + ' |';
                  }).join('\n');
                  return header + '\n' + divider + '\n' + body;
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetBase64Encode,
            Script =
                """
                function transform(input) {
                  // Simple Base64 encode for ASCII/UTF-8 text
                  var chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
                  var bytes = [];
                  for (var i = 0; i < input.length; i++) {
                    var c = input.charCodeAt(i);
                    if (c < 128) { bytes.push(c); }
                    else if (c < 2048) { bytes.push(192 | (c >> 6)); bytes.push(128 | (c & 63)); }
                    else { bytes.push(224 | (c >> 12)); bytes.push(128 | ((c >> 6) & 63)); bytes.push(128 | (c & 63)); }
                  }
                  var result = '';
                  for (var i = 0; i < bytes.length; i += 3) {
                    var b0 = bytes[i], b1 = i+1 < bytes.length ? bytes[i+1] : 0, b2 = i+2 < bytes.length ? bytes[i+2] : 0;
                    result += chars[b0 >> 2] + chars[((b0 & 3) << 4) | (b1 >> 4)];
                    result += (i+1 < bytes.length) ? chars[((b1 & 15) << 2) | (b2 >> 6)] : '=';
                    result += (i+2 < bytes.length) ? chars[b2 & 63] : '=';
                  }
                  return result;
                }
                """
        },
        new MacroItem
        {
            Name = Loc.PresetBase64Decode,
            Script =
                """
                function transform(input) {
                  var chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
                  var s = input.replace(/[^A-Za-z0-9+/=]/g, '');
                  var bytes = [];
                  for (var i = 0; i < s.length; i += 4) {
                    var a = chars.indexOf(s[i]), b = chars.indexOf(s[i+1]);
                    var c = s[i+2] === '=' ? 0 : chars.indexOf(s[i+2]);
                    var d = s[i+3] === '=' ? 0 : chars.indexOf(s[i+3]);
                    bytes.push((a << 2) | (b >> 4));
                    if (s[i+2] !== '=') bytes.push(((b & 15) << 4) | (c >> 2));
                    if (s[i+3] !== '=') bytes.push(((c & 3) << 6) | d);
                  }
                  var result = '';
                  for (var i = 0; i < bytes.length; ) {
                    var b = bytes[i];
                    if (b < 128) { result += String.fromCharCode(b); i++; }
                    else if (b < 224) { result += String.fromCharCode(((b & 31) << 6) | (bytes[i+1] & 63)); i += 2; }
                    else { result += String.fromCharCode(((b & 15) << 12) | ((bytes[i+1] & 63) << 6) | (bytes[i+2] & 63)); i += 3; }
                  }
                  return result;
                }
                """
        }
    };
    /// <summary>
    /// 매크로 스크립트를 실행하여 input 문자열을 변환한다.
    /// </summary>
    public static string RunMacro(string script, string input)
    {
        var engine = new Engine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromMilliseconds(500));
            options.LimitMemory(4_000_000);
            options.MaxStatements(5000);
            options.Strict();
        });

        engine.SetValue("host", new SafeHost());

        engine.Execute(script);

        var result = engine.Invoke("transform", input);

        if (!result.IsString())
            throw new InvalidOperationException(Loc.MacroMustReturnString);

        return result.AsString();
    }

    /// <summary>
    /// 스크립트의 유효성을 검증한다 (간단한 테스트 실행).
    /// </summary>
    public static (bool success, string output, string? error) TestMacro(string script, string testInput)
    {
        try
        {
            var output = RunMacro(script, testInput);
            return (true, output, null);
        }
        catch (ExecutionCanceledException)
        {
            return (false, "", Loc.MacroTimeout);
        }
        catch (MemoryLimitExceededException)
        {
            return (false, "", Loc.MacroMemoryLimit);
        }
        catch (StatementsCountOverflowException)
        {
            return (false, "", Loc.MacroStatementLimit);
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }
}
