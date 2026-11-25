namespace JsonPretty.Core;

/// <summary>정렬 출력 옵션.</summary>
public sealed record FormatOptions
{
    /// <summary>들여쓰기 칸 수 (UseTabs가 false일 때).</summary>
    public int IndentSize { get; init; } = 2;

    /// <summary>true면 들여쓰기에 탭 문자를 사용.</summary>
    public bool UseTabs { get; init; }

    /// <summary>true면 객체의 키를 이름순(서수 비교)으로 정렬.</summary>
    public bool SortKeys { get; init; }

    /// <summary>true면 한글 등 비 ASCII 문자를 \uXXXX로 이스케이프.</summary>
    public bool EscapeNonAscii { get; init; }

    /// <summary>줄바꿈 문자. Windows 기본은 CRLF.</summary>
    public string NewLine { get; init; } = "\r\n";

    public static FormatOptions Default { get; } = new();
}
