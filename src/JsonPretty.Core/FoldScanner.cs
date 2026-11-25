namespace JsonPretty.Core;

/// <summary>접을 수 있는 구간. Start는 여는 괄호 위치, End는 닫는 괄호 바로 다음 위치.</summary>
public sealed record FoldRange(int Start, int End, char Open, int ChildCount);

/// <summary>
/// JSON 텍스트에서 { } / [ ] 쌍을 찾아 코드 접기 구간을 만든다.
/// 문자열 안의 괄호는 무시하며, 짝이 맞지 않는 괄호는 건너뛴다.
/// 한 줄 안에서 끝나는 구간은 접을 필요가 없으므로 제외한다.
/// </summary>
public static class FoldScanner
{
    public static IReadOnlyList<FoldRange> Scan(string text)
    {
        var result = new List<FoldRange>();
        var stack = new Stack<Open>();
        bool inString = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (c == '\\') i++;          // 이스케이프된 다음 글자는 건너뜀
                else if (c == '"') inString = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    MarkValue(stack);
                    break;
                case '{':
                case '[':
                    MarkValue(stack);
                    stack.Push(new Open(i, c));
                    break;
                case '}':
                case ']':
                    char expected = c == '}' ? '{' : '[';
                    if (stack.Count == 0 || stack.Peek().Char != expected)
                        break; // 짝이 맞지 않으면 무시
                    var open = stack.Pop();
                    if (text.IndexOf('\n', open.Offset, i - open.Offset) >= 0)
                        result.Add(new FoldRange(open.Offset, i + 1, open.Char, open.Children));
                    break;
                case ',':
                    if (stack.Count > 0) stack.Peek().PendingValue = false;
                    break;
                default:
                    if (!char.IsWhiteSpace(c) && c != ':')
                        MarkValue(stack);
                    break;
            }
        }

        result.Sort((a, b) => a.Start.CompareTo(b.Start));
        return result;
    }

    /// <summary>
    /// 현재 컨테이너에 값(배열 원소 또는 객체 속성)이 하나 시작됐음을 기록한다.
    /// 객체에서는 키 문자열이 먼저 나오므로 속성 하나당 한 번만 세도록 쉼표 전까지 중복을 막는다.
    /// </summary>
    private static void MarkValue(Stack<Open> stack)
    {
        if (stack.Count == 0) return;
        var top = stack.Peek();
        if (top.PendingValue) return;
        top.PendingValue = true;
        top.Children++;
    }

    private sealed class Open(int offset, char c)
    {
        public int Offset { get; } = offset;
        public char Char { get; } = c;
        public int Children { get; set; }
        public bool PendingValue { get; set; }
    }
}
