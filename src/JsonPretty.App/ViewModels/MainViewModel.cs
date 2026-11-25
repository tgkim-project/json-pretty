using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JsonPretty.App.Services;
using JsonPretty.Core;

namespace JsonPretty.App.ViewModels;

public enum OutputMode { Pretty, Minified }

public sealed record ThemeChoice(string Label, AppTheme Theme);

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

    public MainViewModel() : this(new AppSettings()) { }

    public MainViewModel(AppSettings settings)
    {
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => ReformatNow();

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastText = string.Empty; };

        // 필드에 직접 넣어 변경 콜백(재정렬)이 돌지 않게 한다.
        selectedIndent = IndentChoices.FirstOrDefault(c => settings.UseTabs ? c.UseTabs : !c.UseTabs && c.Size == settings.IndentSize)
                         ?? IndentChoices[0];
        sortKeys = settings.SortKeys;
        allowLenient = settings.Lenient;
        detectClipboard = settings.DetectClipboard;
        selectedTheme = ThemeChoices.FirstOrDefault(t => t.Theme.ToString() == settings.Theme) ?? ThemeChoices[0];
        _lastDirectory = settings.LastDirectory;
    }

    /// <summary>현재 옵션을 설정 객체에 기록 (종료 시 저장용).</summary>
    public void WriteTo(AppSettings settings)
    {
        settings.IndentSize = SelectedIndent.UseTabs ? 2 : SelectedIndent.Size;
        settings.UseTabs = SelectedIndent.UseTabs;
        settings.SortKeys = SortKeys;
        settings.Lenient = AllowLenient;
        settings.DetectClipboard = DetectClipboard;
        settings.Theme = SelectedTheme.Theme.ToString();
        settings.LastDirectory = _lastDirectory;
    }

    // ---------- 테마 ----------

    public IReadOnlyList<ThemeChoice> ThemeChoices { get; } =
    [
        new("시스템", AppTheme.System),
        new("라이트", AppTheme.Light),
        new("다크", AppTheme.Dark),
    ];

    [ObservableProperty] private ThemeChoice selectedTheme;
    partial void OnSelectedThemeChanged(ThemeChoice value) => ThemeService.Apply(value.Theme);

    // ---------- 느슨한 입력 ----------

    [ObservableProperty] private bool allowLenient;
    partial void OnAllowLenientChanged(bool value) => ReformatNow();

    // ---------- 트리 보기 ----------

    private bool _treeDirty = true;

    public JsonTreeModel Tree { get; } = new();

    [ObservableProperty] private bool isTreeTabSelected;
    partial void OnIsTreeTabSelectedChanged(bool value) => RefreshTreeIfVisible();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyTreePathCommand), nameof(CopyTreeValueCommand))]
    private JsonTreeNode? selectedTreeNode;

    partial void OnOutputTextChanged(string value)
    {
        _treeDirty = true;
        RefreshTreeIfVisible();
    }

    /// <summary>트리는 만들 때 비용이 드므로 트리 탭이 보일 때만 다시 만든다.</summary>
    private void RefreshTreeIfVisible()
    {
        if (!IsTreeTabSelected || !_treeDirty) return;
        _treeDirty = false;
        SelectedTreeNode = null;
        Tree.Build(OutputText);
    }

    private bool HasSelectedTreeNode() => SelectedTreeNode is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedTreeNode))]
    private void CopyTreePath() => CopyToClipboard(SelectedTreeNode!.Path, $"경로를 복사했습니다: {SelectedTreeNode.Path}");

    [RelayCommand(CanExecute = nameof(HasSelectedTreeNode))]
    private void CopyTreeValue() =>
        CopyToClipboard(SelectedTreeNode!.GetCopyValue(_formatter, BuildOptions()), "값을 복사했습니다.");

    // ---------- 클립보드 감지 ----------

    private const int ClipboardDetectMaxLength = 5 * 1024 * 1024;
    private string? _lastSeenClipboard;

    [ObservableProperty] private bool detectClipboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClipboardSuggestion))]
    private string? clipboardSuggestion;

    [ObservableProperty] private string clipboardSuggestionInfo = string.Empty;

    public bool HasClipboardSuggestion => ClipboardSuggestion is not null;

    /// <summary>
    /// 창이 활성화될 때 호출. 클립보드에 새 JSON이 있으면 붙여넣기를 제안한다.
    /// 같은 내용은 한 번만 묻고, 이 프로그램이 복사한 내용은 묻지 않는다.
    /// </summary>
    public void CheckClipboard()
    {
        if (!DetectClipboard) return;

        string? text = ClipboardService.TryGetText();
        if (string.IsNullOrWhiteSpace(text) || text == _lastSeenClipboard) return;
        _lastSeenClipboard = text;

        if (text.Length > ClipboardDetectMaxLength || text == _input) return;
        string trimmed = text.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] is not ('{' or '[')) return;

        var result = _formatter.Minify(text, BuildOptions());
        if (!result.Success || result.Stats is null) return;

        int lines = 1;
        foreach (char c in text) if (c == '\n') lines++;
        ClipboardSuggestionInfo = $"클립보드에 JSON이 있습니다 ({lines:N0}줄, {FormatSize(Encoding.UTF8.GetByteCount(text))}). 붙여넣을까요?";
        ClipboardSuggestion = text;
    }

    [RelayCommand]
    private void AcceptClipboardSuggestion()
    {
        if (ClipboardSuggestion is not { } text) return;
        ClipboardSuggestion = null;
        InputReplaceRequested?.Invoke(this, text);
        _input = text;
        CurrentFilePath = null;
        ReformatNow();
    }

    [RelayCommand]
    private void DismissClipboardSuggestion() => ClipboardSuggestion = null;

    [RelayCommand]
    private void DisableClipboardDetection()
    {
        ClipboardSuggestion = null;
        DetectClipboard = false;
        ShowToast("클립보드 감지를 껐습니다. 상단 옵션에서 다시 켤 수 있습니다.");
    }

    private string? _lastDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? currentFilePath;

    /// <summary>csproj의 Version (예: 1.0.0)</summary>
    private static readonly string AppVersion =
        typeof(MainViewModel).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";

    public string WindowTitle => CurrentFilePath is null
        ? $"JSON Pretty {AppVersion}"
        : $"{Path.GetFileName(CurrentFilePath)} - JSON Pretty {AppVersion}";

    [RelayCommand]
    private void OpenFile()
    {
        var path = FileService.PickOpenPath(_lastDirectory);
        if (path is not null)
            LoadFile(path);
    }

    /// <summary>파일을 읽어 입력 창에 넣는다. 열기 대화상자와 드래그앤드롭에서 사용.</summary>
    public void LoadFile(string path)
    {
        string text;
        try
        {
            text = FileService.ReadText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show($"파일을 열 수 없습니다.\n\n{path}\n\n{ex.Message}", "JSON Pretty",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        InputReplaceRequested?.Invoke(this, text);
        _input = text;
        CurrentFilePath = path;
        _lastDirectory = Path.GetDirectoryName(path);
        ReformatNow();
        ShowToast($"{Path.GetFileName(path)} 을(를) 열었습니다.");
    }

    [RelayCommand]
    private void SaveFile()
    {
        if (_debounce.IsEnabled)
            ReformatSync();

        if (LastError is not null)
        {
            ShowToast("JSON 오류가 있어 저장할 수 없습니다. 오류를 먼저 고치세요.");
            return;
        }
        if (string.IsNullOrEmpty(OutputText))
        {
            ShowToast("저장할 내용이 없습니다.");
            return;
        }

        string? suggested = CurrentFilePath is null ? null : Path.GetFileName(CurrentFilePath);
        string? dir = CurrentFilePath is null ? _lastDirectory : Path.GetDirectoryName(CurrentFilePath);
        var path = FileService.PickSavePath(dir, suggested);
        if (path is null) return;

        try
        {
            FileService.WriteText(path, OutputText);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show($"저장하지 못했습니다.\n\n{path}\n\n{ex.Message}", "JSON Pretty",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _lastDirectory = Path.GetDirectoryName(path);
        ShowToast($"{Path.GetFileName(path)} 에 저장했습니다.");
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
        CurrentFilePath = null;
        _lastSeenClipboard = text;
        ClipboardSuggestion = null; // 이미 붙여넣었으니 제안 배너는 닫는다
        ReformatNow();
    }

    [RelayCommand]
    private void Clear()
    {
        InputReplaceRequested?.Invoke(this, string.Empty);
        _input = string.Empty;
        CurrentFilePath = null;
        ReformatNow();
    }

    private void CopyToClipboard(string text, string successMessage)
    {
        if (string.IsNullOrEmpty(text))
        {
            ShowToast("복사할 내용이 없습니다.");
            return;
        }
        bool ok = ClipboardService.TrySetText(text);
        if (ok) _lastSeenClipboard = text; // 직접 복사한 내용은 붙여넣기 제안을 하지 않는다
        ShowToast(ok ? successMessage : "클립보드를 사용할 수 없습니다. 잠시 후 다시 시도하세요.");
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
        Lenient = AllowLenient,
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

        string notice = result.Notice is null ? string.Empty : $"  ({result.Notice})";
        StatusText = $"✔  유효한 JSON{notice}   |   객체 {s.Objects} · 배열 {s.Arrays} · 값 {s.Values} · 깊이 {s.MaxDepth}" +
                     $"   |   {FormatSize(Encoding.UTF8.GetByteCount(result.Output))}   |   {lines:N0}줄";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };
}
