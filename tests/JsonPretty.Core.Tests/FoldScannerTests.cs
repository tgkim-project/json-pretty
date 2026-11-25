using JsonPretty.Core;
using Xunit;

namespace JsonPretty.Core.Tests;

public class FoldScannerTests
{
    [Fact]
    public void Scan_FindsNestedMultiLineContainers()
    {
        string text = "{\n  \"a\": [\n    1,\n    2\n  ],\n  \"b\": {}\n}";
        var folds = FoldScanner.Scan(text);

        Assert.Equal(2, folds.Count);

        Assert.Equal(0, folds[0].Start);
        Assert.Equal(text.Length, folds[0].End);
        Assert.Equal('{', folds[0].Open);
        Assert.Equal(2, folds[0].ChildCount);

        Assert.Equal(text.IndexOf('['), folds[1].Start);
        Assert.Equal(']', text[folds[1].End - 1]);
        Assert.Equal('[', folds[1].Open);
        Assert.Equal(2, folds[1].ChildCount);
    }

    [Fact]
    public void Scan_SkipsSingleLineContainers()
    {
        Assert.Empty(FoldScanner.Scan("{\"a\":[1,2],\"b\":{\"c\":1}}"));
    }

    [Fact]
    public void Scan_IgnoresBracketsInsideStrings()
    {
        string text = "[\n  \"} ] { [\",\n  \"\\\"]\"\n]";
        var folds = FoldScanner.Scan(text);

        var fold = Assert.Single(folds);
        Assert.Equal(0, fold.Start);
        Assert.Equal(text.Length, fold.End);
        Assert.Equal(2, fold.ChildCount);
    }

    [Fact]
    public void Scan_IgnoresUnbalancedBrackets()
    {
        var folds = FoldScanner.Scan("[\n 1\n}\n]");
        var fold = Assert.Single(folds);
        Assert.Equal('[', fold.Open);
    }

    [Fact]
    public void Scan_CountsChildrenOfObjectsAndArrays()
    {
        var folds = FoldScanner.Scan("{\n\"a\": {\"x\":1},\n\"b\": [1,[2,3]],\n\"c\": \"s\",\n\"d\": null\n}");
        Assert.Equal(4, Assert.Single(folds).ChildCount);
    }

    [Fact]
    public void Scan_EmptyText()
    {
        Assert.Empty(FoldScanner.Scan(string.Empty));
    }
}
