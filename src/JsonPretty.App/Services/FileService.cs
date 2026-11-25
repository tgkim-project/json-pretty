using System.IO;
using System.Text;
using Microsoft.Win32;

namespace JsonPretty.App.Services;

public static class FileService
{
    /// <summary>이보다 큰 파일은 에디터가 감당하기 어려우므로 열지 않는다.</summary>
    public const long MaxFileBytes = 100L * 1024 * 1024;

    private const string Filter = "JSON 파일 (*.json)|*.json|텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*";

    public static string? PickOpenPath(string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = "JSON 파일 열기",
            Filter = Filter,
            InitialDirectory = ExistingDirectoryOrNull(initialDirectory),
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public static string? PickSavePath(string? initialDirectory, string? suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "정렬 결과 저장",
            Filter = Filter,
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedFileName ?? "formatted.json",
            InitialDirectory = ExistingDirectoryOrNull(initialDirectory),
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>BOM이 있으면 그 인코딩으로, 없으면 UTF-8로 읽는다.</summary>
    public static string ReadText(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxFileBytes)
            throw new IOException($"파일이 너무 큽니다 ({info.Length / (1024 * 1024)} MB). {MaxFileBytes / (1024 * 1024)} MB 이하만 열 수 있습니다.");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    /// <summary>JSON 표준에 맞춰 BOM 없는 UTF-8로 쓴다.</summary>
    public static void WriteText(string path, string text) =>
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static string? ExistingDirectoryOrNull(string? dir) =>
        !string.IsNullOrEmpty(dir) && Directory.Exists(dir) ? dir : null;
}
