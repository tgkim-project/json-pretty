using System.Text.RegularExpressions;
using JsonPretty.App.Localization;
using Xunit;

namespace JsonPretty.Core.Tests;

/// <summary>화면 문구 표(한국어·영어)가 서로 어긋나지 않았는지 확인.</summary>
public class StringsTests
{
    [Fact]
    public void KoreanAndEnglish_HaveSameKeys()
    {
        var onlyEn = Strings.En.Keys.Except(Strings.Ko.Keys).ToList();
        var onlyKo = Strings.Ko.Keys.Except(Strings.En.Keys).ToList();
        Assert.True(onlyEn.Count == 0, "한국어 문구 없음: " + string.Join(", ", onlyEn));
        Assert.True(onlyKo.Count == 0, "영어 문구 없음: " + string.Join(", ", onlyKo));
    }

    [Fact]
    public void Placeholders_MatchBetweenLanguages()
    {
        foreach (var (key, en) in Strings.En)
        {
            Assert.Equal(Placeholders(en), Placeholders(Strings.Ko[key]));
        }
    }

    [Fact]
    public void AllStrings_AreValidFormatStrings()
    {
        // 자리표시자가 있는 문구는 string.Format에서 예외가 나지 않아야 한다
        var args = Enumerable.Range(0, 10).Select(i => (object)i).ToArray();
        foreach (var table in new[] { Strings.En, Strings.Ko })
            foreach (var (key, value) in table)
            {
                var ex = Record.Exception(() => string.Format(value, args));
                Assert.True(ex is null, $"{key}: {ex?.Message}");
            }
    }

    private static string Placeholders(string s) =>
        string.Join(",", Regex.Matches(s, @"(?<!\{)\{(\d+)").Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x));
}
