using JsonPretty.Core;
using Xunit;

namespace JsonPretty.Core.Tests;

public class JsonFormatterTests
{
    private static readonly JsonFormatter Formatter = new();
    private static readonly FormatOptions Lf = new() { NewLine = "\n" };

    [Fact]
    public void Format_IndentsWithTwoSpaces()
    {
        var r = Formatter.Format("{\"a\":1,\"b\":[true,null]}", Lf);

        Assert.True(r.Success);
        Assert.Equal("{\n  \"a\": 1,\n  \"b\": [\n    true,\n    null\n  ]\n}", r.Output);
    }

    [Fact]
    public void Format_FourSpacesAndTabs()
    {
        Assert.Equal("{\n    \"a\": 1\n}", Formatter.Format("{\"a\":1}", Lf with { IndentSize = 4 }).Output);
        Assert.Equal("{\n\t\"a\": 1\n}", Formatter.Format("{\"a\":1}", Lf with { UseTabs = true }).Output);
    }

    [Fact]
    public void Format_UsesCrLfByDefault()
    {
        Assert.Equal("[\r\n  1\r\n]", Formatter.Format("[1]", FormatOptions.Default).Output);
    }

    [Fact]
    public void Format_EmptyContainersStayOnOneLine()
    {
        Assert.Equal("{\n  \"o\": {},\n  \"a\": []\n}", Formatter.Format("{\"o\":{},\"a\":[]}", Lf).Output);
    }

    [Fact]
    public void Minify_RemovesWhitespace()
    {
        var r = Formatter.Minify("{ \"a\" : [ 1 , 2 ] ,\n \"b\" : \"x y\" }", Lf);
        Assert.Equal("{\"a\":[1,2],\"b\":\"x y\"}", r.Output);
    }

    [Fact]
    public void Format_KeepsKoreanUnescaped()
    {
        Assert.Equal("{\"이름\":\"홍길동\"}", Formatter.Minify("{\"이름\":\"홍길동\"}", Lf).Output);
        Assert.Equal("{\"이름\":\"홍길동\"}", Formatter.Minify("{\"\\uC774\\uB984\":\"홍길동\"}", Lf).Output);
    }

    [Fact]
    public void Format_EscapeNonAscii_WhenRequested()
    {
        var r = Formatter.Minify("{\"a\":\"한\"}", Lf with { EscapeNonAscii = true });
        Assert.Equal("{\"a\":\"\\uD55C\"}", r.Output);
    }

    [Fact]
    public void Format_PreservesNumberText()
    {
        var r = Formatter.Minify("[12345678901234567890, 1.0, 1e10, -0.5]", Lf);
        Assert.Equal("[12345678901234567890,1.0,1e10,-0.5]", r.Output);
    }

    [Fact]
    public void Format_EscapesSpecialCharactersInStrings()
    {
        var r = Formatter.Minify("[\"q\\\"b\\\\n\\n\\t\"]", Lf);
        Assert.Equal("[\"q\\\"b\\\\n\\n\\t\"]", r.Output);
    }

    [Fact]
    public void Format_SortKeys_Recursive()
    {
        var r = Formatter.Minify("{\"b\":1,\"a\":{\"d\":1,\"c\":2}}", Lf with { SortKeys = true });
        Assert.Equal("{\"a\":{\"c\":2,\"d\":1},\"b\":1}", r.Output);
    }

    [Fact]
    public void Format_AllowsCommentsTrailingCommasAndBom()
    {
        var r = Formatter.Minify("\uFEFF{ // c\n \"a\": 1, /* x */ }", Lf);
        Assert.True(r.Success);
        Assert.Equal("{\"a\":1}", r.Output);
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("\"abc\"", "\"abc\"")]
    [InlineData("null", "null")]
    public void Format_ScalarRoot(string input, string expected)
    {
        Assert.Equal(expected, Formatter.Format(input, Lf).Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n ")]
    public void Format_EmptyInput_IsNotAnError(string input)
    {
        var r = Formatter.Format(input, Lf);
        Assert.True(r.Success);
        Assert.Equal(string.Empty, r.Output);
        Assert.Null(r.Error);
    }

    [Fact]
    public void Format_ReportsErrorLineAndColumn()
    {
        var r = Formatter.Format("{\n  \"a\": 1\n  \"b\": 2\n}", Lf);

        Assert.False(r.Success);
        Assert.NotNull(r.Error);
        Assert.Equal(3, r.Error!.Line);
        Assert.Equal(3, r.Error.Column);
        Assert.DoesNotContain("LineNumber", r.Error.Message);
    }

    [Fact]
    public void Format_ErrorColumn_CountsCharactersNotBytes()
    {
        // "한글" 은 UTF-8로 6바이트지만 2글자. 오류는 8번째 글자(x)에서 발생.
        var r = Formatter.Format("{\"한글\": x}", Lf);

        Assert.False(r.Success);
        Assert.Equal(1, r.Error!.Line);
        Assert.Equal(8, r.Error.Column);
    }

    [Fact]
    public void Format_TooDeep_ReturnsError()
    {
        string deep = new string('[', JsonFormatter.MaxDepth + 1) + new string(']', JsonFormatter.MaxDepth + 1);
        Assert.False(Formatter.Format(deep, Lf).Success);
    }

    [Fact]
    public void Stats_CountsStructure()
    {
        var r = Formatter.Format("{\"a\":[1,{\"b\":2}],\"c\":\"x\"}", Lf);
        Assert.Equal(new JsonStats(Objects: 2, Arrays: 1, Values: 3, MaxDepth: 3), r.Stats);
    }
}
