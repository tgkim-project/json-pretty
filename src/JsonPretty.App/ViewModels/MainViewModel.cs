using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JsonPretty.App.Services;
using JsonPretty.Core;

namespace JsonPretty.App.ViewModels;

public enum OutputMode { Pretty, Minified }

public sealed record IndentChoice(string Label, int Size, bool UseTabs)
{
    public override string ToString() => Label;
}

public partial class MainViewModel : ObservableObject
{
    private readonly IJsonFormatter _formatter = new JsonFormatter();
    private readonly DispatcherTimer _debounce;
    private readonly DispatcherTimer _toastTimer;
    private string _input = string.Empty;
    private int _version;

    public MainViewModel()
    {
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => ReformatNow();

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastText = string.Empty; };

        selectedIndent = IndentChoices[0];
    }

    /// <summary>뷰가 입력 에디터 내용을 바꿔야 할 때 (붙여넣기, 지우기).</summary>
    public event EventHandler<string>? InputReplaceRequested;

    public IReadOnlyList<IndentChoice> IndentChoices { get; } =
    [
        new("2칸", 2, false),
        new("4칸", 4, false),
        new("탭", 1, true),
    ];

    [ObservableProperty] private IndentChoice selectedIndent;
    [ObservableProperty] private bool sortKeys;
    [ObservableProperty] private OutputMode mode = OutputMode.Pretty;
    [ObservableProperty] private string outputText = string.Empty;
    [ObservableProperty] private string statusText = "JSON을 붙여넣거나 입력하세요.";
    [ObservableProperty] private FormatError? lastError;
    [ObservableProperty] private string toastText = string.Empty;

    partial void OnSelectedIndentChanged(IndentChoice value) => ReformatNow();
    partial void OnSortKeysChanged(bool value) => ReformatNow();

    /// <summary>입력이 바뀔 때마다 호출. 300ms 동안 추가 입력이 없으면 정렬한다.</summary>
    public void SetInput(string text)
    {
        _input = text;
        _debounce.Stop();
        _debounce.Start();
    }

    [RelayCommand]
    private void Format()
    {
        Mode = OutputMode.Pretty;
        ReformatNow();
    }

    [RelayCommand]
    private void Minify()
    {
        Mode = OutputMode.Minified;
        ReformatNow();
    }

    [RelayCommand]
    private void Copy()
    {
        if (_debounce.IsEnabled)
            ReformatSync(); // 디바운스 대기 중이면 최신 입력으로 먼저 정렬

        if (LastError is not null)
        {
            ShowToast("JSON 오류가 있어 복사하지 않았습니다. 오류를 먼저 고치세요.");
            return;
        }
        CopyToClipboard(OutputText, "정렬 결과를 복사했습니다.");
    }

    [RelayCommand]
    private void CopyMinified()
    {
        var result = _formatter.Minify(_input, BuildOptions());
        if (!result.Success)
        {
            ShowToast("JSON 오류가 있어 압축 복사할 수 없습니다.");
            return;
        }
        CopyToClipboard(result.Output, "한 줄로 압축해 복사했습니다.");
    }

    [RelayCommand]
    private void Paste()
    {
        var text = ClipboardService.TryGetText();
        if (string.IsNullOrEmpty(text))
        {
            ShowToast("클립보드에 텍스트가 없습니다.");
            return;
        }
        InputReplaceRequested?.Invoke(this, text);
        _input = text;
        ReformatNow();
    }

    [RelayCommand]
    private void Clear()
    {
        InputReplaceRequested?.Invoke(this, string.Empty);
        _input = string.Empty;
        ReformatNow();
    }

    private void CopyToClipboard(string text, string successMessage)
    {
        if (string.IsNullOrEmpty(text))
        {
            ShowToast("복사할 내용이 없습니다.");
            return;
        }
        ShowToast(ClipboardService.TrySetText(text)
            ? successMessage
            : "클립보드를 사용할 수 없습니다. 잠시 후 다시 시도하세요.");
    }

    private void ShowToast(string message)
    {
        ToastText = message;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private FormatOptions BuildOptions() => new()
    {
        IndentSize = SelectedIndent.Size,
        UseTabs = SelectedIndent.UseTabs,
        SortKeys = SortKeys,
    };

    private FormatResult Run(string input, FormatOptions options, OutputMode mode) =>
        mode == OutputMode.Pretty ? _formatter.Format(input, options) : _formatter.Minify(input, options);

    private void ReformatSync()
    {
        _debounce.Stop();
        _version++;
        Apply(Run(_input, BuildOptions(), Mode));
    }

    /// <summary>백그라운드에서 정렬하고, 그 사이 더 새로운 요청이 오면 결과를 버린다.</summary>
    private async void ReformatNow()
    {
        _debounce.Stop();
        int version = ++_version;
        string input = _input;
        var options = BuildOptions();
        var mode = Mode;

        var result = await Task.Run(() => Run(input, options, mode));
        if (version == _version)
            Apply(result);
    }

    private void Apply(FormatResult result)
    {
        if (!result.Success)
        {
            // 오류 시 출력은 마지막 성공 결과를 유지한다.
            LastError = result.Error;
            StatusText = $"✖  {result.Error!.Line}행 {result.Error.Column}열: {result.Error.Message}  (클릭하면 이동)";
            return;
        }

        LastError = null;
        OutputText = result.Output;

        if (result.Stats is not { } s)
        {
            StatusText = "JSON을 붙여넣거나 입력하세요.";
            return;
        }

        int lines = 1;
        foreach (char c in result.Output) if (c == '\n') lines++;

        StatusText = $"✔  유효한 JSON   |   객체 {s.Objects} · 배열 {s.Arrays} · 값 {s.Values} · 깊이 {s.MaxDepth}" +
                     $"   |   {FormatSize(Encoding.UTF8.GetByteCount(result.Output))}   |   {lines:N0}줄";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };
}
