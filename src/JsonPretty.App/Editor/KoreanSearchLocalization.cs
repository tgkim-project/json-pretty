using ICSharpCode.AvalonEdit.Search;

namespace JsonPretty.App.Editor;

/// <summary>AvalonEdit 찾기 패널(Ctrl+F)의 한국어 문구.</summary>
public sealed class KoreanSearchLocalization : Localization
{
    public static KoreanSearchLocalization Instance { get; } = new();

    public override string MatchCaseText => "대/소문자 구분";
    public override string MatchWholeWordsText => "단어 단위로";
    public override string UseRegexText => "정규식 사용";
    public override string FindNextText => "다음 찾기 (F3)";
    public override string FindPreviousText => "이전 찾기 (Shift+F3)";
    public override string ErrorText => "오류: ";
    public override string NoMatchesFoundText => "찾는 내용이 없습니다";
}
