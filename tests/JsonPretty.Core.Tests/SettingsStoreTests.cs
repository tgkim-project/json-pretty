using System.IO;
using JsonPretty.Core;
using Xunit;

namespace JsonPretty.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "JsonPrettyTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(2, s.IndentSize);
        Assert.False(s.SortKeys);
        Assert.Null(s.WindowWidth);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_AndCreatesDirectory()
    {
        var store = new SettingsStore(FilePath);
        Assert.True(store.Save(new AppSettings
        {
            IndentSize = 4,
            UseTabs = true,
            SortKeys = true,
            WindowLeft = 10, WindowTop = 20, WindowWidth = 900, WindowHeight = 600,
            WindowMaximized = true,
            SplitRatio = 0.4,
            LastDirectory = @"C:\데이터",
        }));

        var s = store.Load();
        Assert.Equal(4, s.IndentSize);
        Assert.True(s.UseTabs);
        Assert.True(s.SortKeys);
        Assert.Equal(10, s.WindowLeft);
        Assert.Equal(900, s.WindowWidth);
        Assert.True(s.WindowMaximized);
        Assert.Equal(0.4, s.SplitRatio);
        Assert.Equal(@"C:\데이터", s.LastDirectory);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");
        Assert.Equal(2, new SettingsStore(FilePath).Load().IndentSize);
    }

    [Fact]
    public void Load_InvalidValues_AreReset()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{\"IndentSize\":7,\"SplitRatio\":5,\"WindowWidth\":10,\"WindowHeight\":10}");

        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(2, s.IndentSize);
        Assert.Null(s.SplitRatio);
        Assert.Null(s.WindowWidth);
        Assert.Null(s.WindowHeight);
    }
}
