using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonPretty.Core;

/// <summary>프로그램을 다시 켜도 유지할 설정.</summary>
public sealed class AppSettings
{
    public int IndentSize { get; set; } = 2;
    public bool UseTabs { get; set; }
    public bool SortKeys { get; set; }
    public bool Lenient { get; set; } = true;
    public bool DetectClipboard { get; set; } = true;

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    /// <summary>입력/출력 창 너비 비율 (왼쪽 비율, 0~1).</summary>
    public double? SplitRatio { get; set; }

    public string? LastDirectory { get; set; }
}

/// <summary>
/// 설정을 JSON 파일로 읽고 쓴다. 파일이 없거나 깨져 있으면 기본값을 쓴다.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public SettingsStore(string path) => Path = path;

    public string Path { get; }

    /// <summary>기본 위치: %APPDATA%\JsonPretty\settings.json</summary>
    public static SettingsStore CreateDefault() => new(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JsonPretty", "settings.json"));

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), JsonOptions) ?? new AppSettings();
            return Sanitize(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>저장 실패는 프로그램 사용에 지장이 없으므로 false만 돌려준다.</summary>
    public bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            // 쓰는 도중 꺼져도 기존 파일이 깨지지 않게 임시 파일에 쓴 뒤 교체한다.
            string temp = Path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static AppSettings Sanitize(AppSettings s)
    {
        if (s.IndentSize is not (2 or 4)) s.IndentSize = 2;
        if (s.SplitRatio is { } r && (double.IsNaN(r) || r < 0.1 || r > 0.9)) s.SplitRatio = null;
        if (s.WindowWidth is < 200 || s.WindowHeight is < 150) s.WindowWidth = s.WindowHeight = null;
        return s;
    }
}
