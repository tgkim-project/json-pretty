namespace JsonPretty.Core;

/// <summary>파싱 오류 위치. Line/Column은 1부터 시작.</summary>
public sealed record FormatError(int Line, int Column, string Message);

/// <summary>JSON 구조 통계.</summary>
public sealed record JsonStats(int Objects, int Arrays, int Values, int MaxDepth);

public sealed record FormatResult(bool Success, string Output, FormatError? Error, JsonStats? Stats)
{
    public static FormatResult Ok(string output, JsonStats stats) => new(true, output, null, stats);

    public static FormatResult Fail(FormatError error) => new(false, string.Empty, error, null);

    /// <summary>입력이 비어 있을 때. 오류는 아니지만 출력도 없음.</summary>
    public static FormatResult Empty { get; } = new(true, string.Empty, null, null);
}
