namespace JsonPretty.Core;

/// <summary>파싱 오류 위치. Line/Column은 1부터 시작. Message는 System.Text.Json의 원문(영어).</summary>
public sealed record FormatError(int Line, int Column, string Message);

/// <summary>JSON 구조 통계.</summary>
public sealed record JsonStats(int Objects, int Arrays, int Values, int MaxDepth);

/// <summary>입력을 자동으로 고쳤을 때 어떤 처리를 했는지. 문구는 UI가 현재 언어로 만든다.</summary>
[Flags]
public enum FormatNotices
{
    None = 0,
    /// <summary>작은따옴표·따옴표 없는 키 등 느슨한 입력을 표준 JSON으로 보정함</summary>
    LenientFixed = 1,
    /// <summary>최상위 문자열 안의 JSON을 풀었음 (횟수는 UnwrapCount)</summary>
    Unwrapped = 2,
}

/// <param name="Notices">입력을 자동으로 고친 내용 (없으면 None).</param>
/// <param name="UnwrapCount">문자열 안 JSON을 푼 횟수.</param>
public sealed record FormatResult(
    bool Success,
    string Output,
    FormatError? Error,
    JsonStats? Stats,
    FormatNotices Notices = FormatNotices.None,
    int UnwrapCount = 0)
{
    public static FormatResult Ok(string output, JsonStats stats, FormatNotices notices = FormatNotices.None, int unwrapCount = 0) =>
        new(true, output, null, stats, notices, unwrapCount);

    public static FormatResult Fail(FormatError error) => new(false, string.Empty, error, null);

    /// <summary>입력이 비어 있을 때. 오류는 아니지만 출력도 없음.</summary>
    public static FormatResult Empty { get; } = new(true, string.Empty, null, null);
}
