using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace JsonPretty.Core;

public interface IJsonFormatter
{
    FormatResult Format(string input, FormatOptions options);
    FormatResult Minify(string input, FormatOptions options);
}

/// <summary>
/// System.Text.Json으로 파싱한 뒤 직접 다시 써서 정렬/압축한다.
/// 숫자는 원문 그대로(GetRawText) 보존해 큰 정수나 1.0 같은 표기가 바뀌지 않게 한다.
/// </summary>
public sealed class JsonFormatter : IJsonFormatter
{
    public const int MaxDepth = 256;

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = MaxDepth,
    };

    public FormatResult Format(string input, FormatOptions options) => Run(input, options, compact: false);

    public FormatResult Minify(string input, FormatOptions options) => Run(input, options, compact: true);

    private static FormatResult Run(string input, FormatOptions options, bool compact)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(options);

        if (input.Length > 0 && input[0] == '﻿')
            input = input[1..];

        if (string.IsNullOrWhiteSpace(input))
            return FormatResult.Empty;

        var notices = new List<string>();

        if (!TryParse(input, out var doc, out var error))
        {
            // 표준 JSON이 아니면 느슨한 입력(작은따옴표, 따옴표 없는 키 등)으로 보고 한 번 더 시도한다.
            // 실패하면 원래 입력 기준의 오류를 돌려준다.
            if (!options.Lenient) return FormatResult.Fail(error!);
            var (strict, map) = LenientJson.ToStrictWithMap(input);
            if (strict == input) return FormatResult.Fail(error!);
            if (!TryParse(strict, out doc, out var strictError))
            {
                // 보정 후에도 남는 오류가 진짜 문제이므로 그 위치를 원본 기준으로 되돌려 알려 준다.
                int strictOffset = OffsetOf(strict, strictError!.Line, strictError.Column);
                int sourceOffset = map[Math.Clamp(strictOffset, 0, map.Length - 1)];
                var (line, column) = PositionOf(input, sourceOffset);
                return FormatResult.Fail(strictError with { Line = line, Column = column });
            }
            notices.Add("느슨한 입력을 표준 JSON으로 보정함");
        }

        // 로그 등에서 흔한 "{\"a\":1}" 처럼 문자열 안에 JSON이 들어 있으면 풀어 준다(중첩 이스케이프 포함).
        if (options.UnwrapEmbeddedJson)
        {
            int unwrapped = 0;
            while (unwrapped < MaxUnwrap && TryUnwrap(doc!, out var inner))
            {
                doc!.Dispose();
                doc = inner;
                unwrapped++;
            }
            if (unwrapped > 0)
                notices.Add(unwrapped == 1 ? "문자열 안의 JSON을 풀었음" : $"문자열 안의 JSON을 {unwrapped}번 풀었음");
        }

        using (doc)
        {
            var writer = new Writer(options, compact);
            writer.Write(doc!.RootElement, 0);
            return FormatResult.Ok(writer.ToString(), writer.Stats, notices.Count > 0 ? string.Join(", ", notices) : null);
        }
    }

    private const int MaxUnwrap = 5;

    /// <summary>1부터 시작하는 줄/열 → 문자 위치.</summary>
    private static int OffsetOf(string text, int line, int column)
    {
        int offset = 0;
        for (int l = 1; l < line; l++)
        {
            int nl = text.IndexOf('\n', offset);
            if (nl < 0) return text.Length;
            offset = nl + 1;
        }
        return Math.Min(offset + column - 1, text.Length);
    }

    /// <summary>문자 위치 → 1부터 시작하는 줄/열.</summary>
    private static (int Line, int Column) PositionOf(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        int line = 1, lineStart = 0;
        for (int k = 0; k < offset; k++)
        {
            if (text[k] == '\n')
            {
                line++;
                lineStart = k + 1;
            }
        }
        return (line, offset - lineStart + 1);
    }

    private static bool TryParse(string text, out JsonDocument? doc, out FormatError? error)
    {
        try
        {
            doc = JsonDocument.Parse(text, ParseOptions);
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            doc = null;
            error = ToError(text, ex);
            return false;
        }
    }

    private static bool TryUnwrap(JsonDocument doc, out JsonDocument? inner)
    {
        inner = null;
        if (doc.RootElement.ValueKind != JsonValueKind.String) return false;

        string s = doc.RootElement.GetString()!.Trim();
        bool looksLikeContainer = s.Length >= 2 &&
            ((s[0] == '{' && s[^1] == '}') || (s[0] == '[' && s[^1] == ']') || (s[0] == '"' && s[^1] == '"'));
        if (!looksLikeContainer) return false;

        return TryParse(s, out inner, out _);
    }

    private static FormatError ToError(string input, JsonException ex)
    {
        int lineIndex = (int)(ex.LineNumber ?? 0);
        long bytePos = ex.BytePositionInLine ?? 0;

        string message = ex.Message;
        int cut = message.IndexOf(" Path:", StringComparison.Ordinal);
        if (cut < 0) cut = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        if (cut > 0) message = message[..cut];
        message = message.TrimEnd(' ', '|');

        // BytePositionInLine은 UTF-8 바이트 기준이므로 문자 기준 열로 변환한다.
        string line = GetLine(input, lineIndex);
        int column = ByteOffsetToCharIndex(line, bytePos) + 1;

        return new FormatError(lineIndex + 1, column, message);
    }

    private static string GetLine(string text, int lineIndex)
    {
        int start = 0;
        for (int i = 0; i < lineIndex; i++)
        {
            int nl = text.IndexOf('\n', start);
            if (nl < 0) return string.Empty;
            start = nl + 1;
        }
        int end = text.IndexOf('\n', start);
        if (end < 0) end = text.Length;
        return text[start..end].TrimEnd('\r');
    }

    private static int ByteOffsetToCharIndex(string line, long byteOffset)
    {
        long bytes = 0;
        int i = 0;
        while (i < line.Length && bytes < byteOffset)
        {
            int len = char.IsSurrogatePair(line, i) ? 2 : 1;
            bytes += Encoding.UTF8.GetByteCount(line.AsSpan(i, len));
            i += len;
        }
        return i;
    }

    private sealed class Writer
    {
        private readonly StringBuilder _sb = new();
        private readonly FormatOptions _options;
        private readonly bool _compact;
        private readonly JavaScriptEncoder _encoder;
        private int _objects, _arrays, _values, _maxDepth;

        public Writer(FormatOptions options, bool compact)
        {
            _options = options;
            _compact = compact;
            _encoder = options.EscapeNonAscii ? JavaScriptEncoder.Default : JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        }

        public JsonStats Stats => new(_objects, _arrays, _values, _maxDepth);

        public override string ToString() => _sb.ToString();

        public void Write(JsonElement element, int depth)
        {
            if (depth > _maxDepth) _maxDepth = depth;

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    _objects++;
                    WriteObject(element, depth);
                    break;
                case JsonValueKind.Array:
                    _arrays++;
                    WriteArray(element, depth);
                    break;
                case JsonValueKind.String:
                    _values++;
                    WriteString(element.GetString()!);
                    break;
                default: // Number, True, False, Null
                    _values++;
                    _sb.Append(element.GetRawText());
                    break;
            }
        }

        private void WriteObject(JsonElement element, int depth)
        {
            IEnumerable<JsonProperty> props = element.EnumerateObject();
            if (_options.SortKeys)
                props = props.OrderBy(p => p.Name, StringComparer.Ordinal);

            _sb.Append('{');
            bool first = true;
            foreach (var prop in props)
            {
                if (!first) _sb.Append(',');
                first = false;
                NewLine(depth + 1);
                WriteString(prop.Name);
                _sb.Append(_compact ? ":" : ": ");
                Write(prop.Value, depth + 1);
            }
            if (!first) NewLine(depth);
            _sb.Append('}');
        }

        private void WriteArray(JsonElement element, int depth)
        {
            _sb.Append('[');
            bool first = true;
            foreach (var item in element.EnumerateArray())
            {
                if (!first) _sb.Append(',');
                first = false;
                NewLine(depth + 1);
                Write(item, depth + 1);
            }
            if (!first) NewLine(depth);
            _sb.Append(']');
        }

        private void WriteString(string value)
        {
            _sb.Append('"');
            _sb.Append(JsonEncodedText.Encode(value, _encoder).ToString());
            _sb.Append('"');
        }

        private void NewLine(int depth)
        {
            if (_compact) return;
            _sb.Append(_options.NewLine);
            if (_options.UseTabs)
                _sb.Append('\t', depth);
            else
                _sb.Append(' ', depth * _options.IndentSize);
        }
    }
}
