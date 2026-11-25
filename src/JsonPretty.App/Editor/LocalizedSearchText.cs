using ICSharpCode.AvalonEdit.Search;
using JsonPretty.App.Localization;

namespace JsonPretty.App.Editor;

/// <summary>AvalonEdit 찾기 패널(Ctrl+F)의 문구를 현재 언어로 보여준다.</summary>
public sealed class LocalizedSearchText : ICSharpCode.AvalonEdit.Search.Localization
{
    public static LocalizedSearchText Instance { get; } = new();

    public override string MatchCaseText => Loc.T("Search_MatchCase");
    public override string MatchWholeWordsText => Loc.T("Search_WholeWords");
    public override string UseRegexText => Loc.T("Search_Regex");
    public override string FindNextText => Loc.T("Search_Next");
    public override string FindPreviousText => Loc.T("Search_Previous");
    public override string ErrorText => Loc.T("Search_Error");
    public override string NoMatchesFoundText => Loc.T("Search_NoMatches");
}
