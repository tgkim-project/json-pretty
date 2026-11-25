using System.Text;

namespace JsonPretty.Core;

/// <summary>
/// 표준이 아니지만 흔히 보이는 JSON 비슷한 입력을 표준 JSON 텍스트로 바꾼다.
/// - 작은따옴표 문자열: 'abc' → "abc"
/// - 따옴표 없는 키: {name: 1} → {"name": 1}
/// - 파이썬 표기: True / False / None → true / false / null
/// 주석과 끝 쉼표는 파서가 이미 허용하므로 그대로 둔다(주석 안의 글자는 건드리지 않는다).
/// 바꾼 결과의 각 글자가 원본 몇 번째 글자에서 왔는지 기록해, 오류 위치를 원본 기준으로 되돌릴 수 있다.
/// </summary>
public static class LenientJson
{
    public static string ToStrict(string text) => Convert(text, trackMap: false).Text;

    /// <summary>변환 결과와, 결과의 각 위치 → 원본 위치 대응표(길이 = 결과 길이 + 1).</summary>
    public static (string Text, int[] SourceIndex) ToStrictWithMap(string text)
    {
        var r = Convert(text, trackMap: true);
        return (r.Text, r.Map!);
    }

    private static (string Text, int[]? Map) Convert(string text, bool trackMap)
    {
        var o = new Output(text.Length, trackMap);
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (c == '"')
            {
                i = CopyDoubleQuoted(text, i, o);
            }
            else if (c == '\'')
            {
                i = ConvertSingleQuoted(text, i, o);
            }
            else if (c == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*'))
            {
                i = CopyComment(text, i, o);
            }
            else if (IsIdentifierStart(c))
            {
                int start = i;
                while (i < text.Length && IsIdentifierPart(text[i])) i++;
                string word = text[start..i];
                bool isKey = NextNonSpaceIs(text, i, ':');

                string? literal = word switch
                {
                    "true" or "false" or "null" => word,
                    "True" => "true",
                    "False" => "false",
                    "None" => "null",
                    _ => null,
                };

                if (literal is not null && !isKey)
                {
                    o.Append(literal, start);
                }
                else if (isKey)
                {
                    o.Append('"', start);          // 따옴표 없는 키
                    o.Append(word, start);
                    o.Append('"', i - 1);
                }
                else
                {
                    o.Append(word, start);         // 알 수 없는 단어는 그대로 두어 파서가 오류를 내게 한다
                }
            }
            else
            {
                o.Append(c, i);
                i++;
            }
        }

        return o.Finish(text.Length);
    }

    private static int CopyDoubleQuoted(string text, int i, Output o)
    {
        o.Append('"', i);
        i++;
        while (i < text.Length)
        {
            char c = text[i];
            o.Append(c, i);
            i++;
            if (c == '\\' && i < text.Length)
            {
                o.Append(text[i], i);
                i++;
            }
            else if (c == '"')
            {
                break;
            }
        }
        return i;
    }

    private static int ConvertSingleQuoted(string text, int i, Output o)
    {
        o.Append('"', i);
        i++;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                char next = text[i + 1];
                if (next == '\'')
                {
                    o.Append('\'', i);            // \' 는 JSON에서 필요 없는 이스케이프
                }
                else
                {
                    o.Append(c, i);
                    o.Append(next, i + 1);
                }
                i += 2;
            }
            else if (c == '"')
            {
                o.Append('\\', i);               // 작은따옴표 문자열 안의 큰따옴표는 이스케이프
                o.Append('"', i);
                i++;
            }
            else if (c == '\'')
            {
                o.Append('"', i);
                return i + 1;
            }
            else
            {
                o.Append(c, i);
                i++;
            }
        }
        o.Append('"', text.Length); // 닫히지 않은 문자열
        return i;
    }

    private static int CopyComment(string text, int i, Output o)
    {
        bool block = text[i + 1] == '*';
        int end = block
            ? text.IndexOf("*/", i + 2, StringComparison.Ordinal)
            : text.IndexOf('\n', i + 2);
        end = end < 0 ? text.Length : (block ? end + 2 : end);
        for (int k = i; k < end; k++) o.Append(text[k], k);
        return end;
    }

    private static bool NextNonSpaceIs(string text, int i, char expected)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return i < text.Length && text[i] == expected;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_' || c == '$';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';

    private sealed class Output(int capacity, bool trackMap)
    {
        private readonly StringBuilder _sb = new(capacity + 16);
        private readonly List<int>? _map = trackMap ? new List<int>(capacity + 16) : null;

        public void Append(char c, int sourceIndex)
        {
            _sb.Append(c);
            _map?.Add(sourceIndex);
        }

        public void Append(string s, int sourceStart)
        {
            for (int k = 0; k < s.Length; k++) Append(s[k], sourceStart + k);
        }

        public (string, int[]?) Finish(int sourceLength)
        {
            _map?.Add(sourceLength);
            return (_sb.ToString(), _map?.ToArray());
        }
    }
}
