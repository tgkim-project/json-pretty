using JsonPretty.Core;
using Xunit;

namespace JsonPretty.Core.Tests;

public class LenientJsonTests
{
    private static readonly JsonFormatter Formatter = new();
    private static readonly FormatOptions Lf = new() { NewLine = "\n" };

    [Theory]
    [InlineData("{'a': 'b'}", "{\"a\":\"b\"}")]
    [InlineData("{a: 1, b_2: [True, False, None]}", "{\"a\":1,\"b_2\":[true,false,null]}")]
    [InlineData("{'say': 'he said \"hi\"'}", "{\"say\":\"he said \\\"hi\\\"\"}")]
    [InlineData("{'it': 'it\\'s'}", "{\"it\":\"it's\"}")]
    [InlineData("{이름: '홍길동'}", "{\"이름\":\"홍길동\"}")]
    [InlineData("{\n  a: 1, // 'not a string'\n  b: 2,\n}", "{\"a\":1,\"b\":2}")]
    public void Lenient_InputIsCorrected(string input, string expected)
    {
        var r = Formatter.Minify(input, Lf);
        Assert.True(r.Success, r.Error?.Message);
        Assert.Equal(expected, r.Output);
        Assert.True(r.Notices.HasFlag(FormatNotices.LenientFixed));
    }

    [Fact]
    public void Lenient_DoesNotTouchDoubleQuotedStrings()
    {
        Assert.Equal("{\"a\":\"True 'x' None\"}", LenientJson.ToStrict("{\"a\":\"True 'x' None\"}"));
    }

    [Fact]
    public void StrictInput_HasNoNotice()
    {
        var r = Formatter.Minify("{\"a\":1}", Lf);
        Assert.Equal(FormatNotices.None, r.Notices);
    }

    [Fact]
    public void Lenient_Disabled_ReturnsError()
    {
        Assert.False(Formatter.Minify("{a: 1}", Lf with { Lenient = false }).Success);
    }

    [Fact]
    public void Lenient_StillInvalid_ReportsRemainingErrorAtSourcePosition()
    {
        // 보정 후 남는 오류(oops)를, 따옴표가 추가되기 전 원본 위치(2행 5열)로 알려 준다.
        var r = Formatter.Format("{a: 1,\n b: oops}", Lf);
        Assert.False(r.Success);
        Assert.Equal(2, r.Error!.Line);
        Assert.Equal(5, r.Error.Column);
    }

    [Fact]
    public void Lenient_MapTracksSourcePositions()
    {
        var (text, map) = LenientJson.ToStrictWithMap("{a:'x'}");
        Assert.Equal("{\"a\":\"x\"}", text);
        Assert.Equal(text.Length + 1, map.Length);
        Assert.Equal(1, map[text.IndexOf('a')]);
        Assert.Equal(4, map[text.IndexOf('x')]);
    }

    [Fact]
    public void Unwrap_EmbeddedJsonString()
    {
        var r = Formatter.Minify("\"{\\\"a\\\":[1,2]}\"", Lf);
        Assert.True(r.Success);
        Assert.Equal("{\"a\":[1,2]}", r.Output);
        Assert.True(r.Notices.HasFlag(FormatNotices.Unwrapped));
        Assert.Equal(1, r.UnwrapCount);
    }

    [Fact]
    public void Unwrap_DoubleEscaped()
    {
        // 두 번 문자열로 감싼 JSON
        var r = Formatter.Minify("\"\\\"{\\\\\\\"a\\\\\\\":1}\\\"\"", Lf);
        Assert.True(r.Success, r.Error?.Message);
        Assert.Equal("{\"a\":1}", r.Output);
        Assert.Equal(2, r.UnwrapCount);
    }

    [Fact]
    public void Unwrap_PlainStringIsLeftAlone()
    {
        var r = Formatter.Minify("\"{not json}\"", Lf);
        Assert.Equal("\"{not json}\"", r.Output);
        Assert.Equal(FormatNotices.None, r.Notices);
    }

    [Fact]
    public void Unwrap_Disabled()
    {
        var r = Formatter.Minify("\"{\\\"a\\\":1}\"", Lf with { UnwrapEmbeddedJson = false });
        Assert.Equal("\"{\\\"a\\\":1}\"", r.Output);
    }
}

public class JsonPathTests
{
    [Theory]
    [InlineData("name", "$.name")]
    [InlineData("_id", "$._id")]
    [InlineData("이름", "$.이름")]
    [InlineData("first name", "$['first name']")]
    [InlineData("a.b", "$['a.b']")]
    [InlineData("it's", "$['it\\'s']")]
    [InlineData("1st", "$['1st']")]
    [InlineData("", "$['']")]
    public void Property(string name, string expected)
    {
        Assert.Equal(expected, JsonPath.Property(JsonPath.Root, name));
    }

    [Fact]
    public void Nested()
    {
        string p = JsonPath.Property(JsonPath.Index(JsonPath.Property(JsonPath.Root, "items"), 2), "name");
        Assert.Equal("$.items[2].name", p);
    }
}
