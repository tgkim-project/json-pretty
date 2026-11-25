using System.Text;

namespace JsonPretty.Core;

/// <summary>
/// 노드 경로 문자열을 만든다. 예: $.items[2].name, $['키 이름'], $["it's"]
/// 식별자로 쓸 수 있는 키는 점 표기, 나머지는 대괄호 표기를 쓴다.
/// </summary>
public static class JsonPath
{
    public const string Root = "$";

    public static string Property(string parentPath, string name)
    {
        if (IsSimpleIdentifier(name))
            return $"{parentPath}.{name}";

        var sb = new StringBuilder(parentPath.Length + name.Length + 6);
        sb.Append(parentPath).Append("['");
        foreach (char c in name)
        {
            if (c is '\'' or '\\') sb.Append('\\');
            sb.Append(c);
        }
        sb.Append("']");
        return sb.ToString();
    }

    public static string Index(string parentPath, int index) => $"{parentPath}[{index}]";

    private static bool IsSimpleIdentifier(string name)
    {
        if (name.Length == 0) return false;
        if (!(char.IsLetter(name[0]) || name[0] is '_' or '$')) return false;
        foreach (char c in name)
            if (!(char.IsLetterOrDigit(c) || c is '_' or '$')) return false;
        return true;
    }
}
