using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Search;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using JsonPretty.App.Editor;
using JsonPretty.App.Services;
using JsonPretty.App.ViewModels;
using JsonPretty.Core;

namespace JsonPretty.App;

public partial class MainWindow : Window
{
    private readonly FoldingManager _outputFolding;
    private readonly ErrorMarkerRenderer _errorMarker;
    private readonly SettingsStore _settingsStore = SettingsStore.CreateDefault();
    private readonly AppSettings _settings;

    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        // 테마(색 리소스)는 XAML의 DynamicResource가 찾을 수 있도록 화면을 만들기 전에 적용한다.
        _settings = _settingsStore.Load();
        ThemeService.Initialize(Enum.TryParse<AppTheme>(_settings.Theme, out var theme) ? theme : AppTheme.System);

        InitializeComponent();

        DataContext = new MainViewModel(_settings);
        RestoreLayout(_settings);
        Closing += (_, _) => SaveSettings();

        var highlighting = ThemeService.GetJsonHighlighting();
        InputEditor.SyntaxHighlighting = highlighting;
        OutputEditor.SyntaxHighlighting = highlighting;

        // 테마가 바뀌면 구문 강조 색이 바뀌었으므로 에디터를 다시 그린다.
        ThemeService.Applied += (_, _) =>
        {
            InputEditor.TextArea.TextView.Redraw();
            OutputEditor.TextArea.TextView.Redraw();
        };

        _outputFolding = FoldingManager.Install(OutputEditor.TextArea);

        // Ctrl+F 찾기 (F3 / Shift+F3 다음·이전)
        SearchPanel.Install(InputEditor).Localization = KoreanSearchLocalization.Instance;
        SearchPanel.Install(OutputEditor).Localization = KoreanSearchLocalization.Instance;

        // 다른 프로그램에서 JSON을 복사해 돌아오면 붙여넣기를 제안
        Activated += (_, _) => Vm.CheckClipboard();
        _errorMarker = new ErrorMarkerRenderer(InputEditor.TextArea.TextView);

        // AvalonEdit의 Text는 의존성 속성이 아니라서 코드 비하인드에서 연결한다.
        InputEditor.TextChanged += (_, _) => Vm.SetInput(InputEditor.Text);
        Vm.InputReplaceRequested += (_, text) =>
        {
            InputEditor.Text = text;
            InputEditor.CaretOffset = 0;
            // 텍스트 교체 직후에는 레이아웃이 끝나지 않아 스크롤이 먹지 않으므로 한 박자 늦춘다.
            Dispatcher.BeginInvoke(() =>
            {
                InputEditor.ScrollToHome();
                InputEditor.ScrollToHorizontalOffset(0);
            }, DispatcherPriority.Loaded);
        };
        Vm.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += (_, _) =>
        {
            InputEditor.Focus();

            // "JsonPretty.exe 파일.json" 으로 실행하면 그 파일을 연다 (탐색기 연결 프로그램, exe에 끌어다 놓기).
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && System.IO.File.Exists(args[1]))
                Vm.LoadFile(args[1]);
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.OutputText):
                OutputEditor.Text = Vm.OutputText;
                JsonFoldingStrategy.Update(_outputFolding, OutputEditor.Document);
                OutputEditor.ScrollToHome();
                break;

            case nameof(MainViewModel.LastError):
                if (Vm.LastError is { } err)
                    _errorMarker.SetError(err.Line, err.Column);
                else
                    _errorMarker.SetError(null, 0);
                break;
        }
    }

    private void RestoreLayout(AppSettings s)
    {
        if (s is { WindowLeft: { } left, WindowTop: { } top, WindowWidth: { } width, WindowHeight: { } height })
        {
            // 모니터 구성이 바뀌어 화면 밖이 되었으면 복원하지 않는다(기본: 화면 가운데).
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                                  SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            var saved = new Rect(left, top, width, height);
            var visible = Rect.Intersect(screen, saved);
            if (!visible.IsEmpty && visible.Width >= 100 && visible.Height >= 50)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
                Width = width;
                Height = height;
            }
        }

        if (s.WindowMaximized)
            WindowState = WindowState.Maximized;

        if (s.SplitRatio is { } ratio)
        {
            InputColumn.Width = new GridLength(ratio, GridUnitType.Star);
            OutputColumn.Width = new GridLength(1 - ratio, GridUnitType.Star);
        }
    }

    private void SaveSettings()
    {
        Vm.WriteTo(_settings);

        // 최대화 상태에서도 복원 크기를 저장해야 다음에 최대화를 풀 때 크기가 맞다.
        Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!bounds.IsEmpty)
        {
            _settings.WindowLeft = bounds.Left;
            _settings.WindowTop = bounds.Top;
            _settings.WindowWidth = bounds.Width;
            _settings.WindowHeight = bounds.Height;
        }
        _settings.WindowMaximized = WindowState == WindowState.Maximized;

        double total = InputColumn.ActualWidth + OutputColumn.ActualWidth;
        if (total > 0)
            _settings.SplitRatio = InputColumn.ActualWidth / total;

        _settingsStore.Save(_settings);
    }

    private static string? GetDroppedFile(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files
            ? files[0]
            : null;

    /// <summary>파일을 끌어 오면 복사 커서를 보여 준다. 텍스트 끌어오기는 에디터 기본 동작에 맡긴다.</summary>
    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (GetDroppedFile(e) is not { } path) return;
        e.Handled = true;
        Activate();
        Vm.LoadFile(path);
    }

    private void OutputTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 탭 안의 콤보박스 등에서 올라온 이벤트는 무시
        if (!ReferenceEquals(e.OriginalSource, OutputTabs)) return;
        Vm.IsTreeTabSelected = OutputTabs.SelectedIndex == 1;
    }

    private void JsonTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        Vm.SelectedTreeNode = e.NewValue as JsonTreeNode;

    /// <summary>우클릭한 항목을 먼저 선택해서 메뉴가 그 노드에 대해 동작하게 한다.</summary>
    private void JsonTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null and not TreeViewItem)
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        if (source is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void FoldAll_Click(object sender, RoutedEventArgs e) => JsonFoldingStrategy.SetAll(_outputFolding, folded: true);

    private void UnfoldAll_Click(object sender, RoutedEventArgs e) => JsonFoldingStrategy.SetAll(_outputFolding, folded: false);

    /// <summary>오류 메시지를 클릭하면 입력 창의 해당 위치로 이동.</summary>
    private void StatusLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Vm.LastError is not { } error) return;

        var doc = InputEditor.Document;
        int line = Math.Clamp(error.Line, 1, doc.LineCount);
        var docLine = doc.GetLineByNumber(line);
        int column = Math.Clamp(error.Column, 1, docLine.Length + 1);

        InputEditor.TextArea.ClearSelection();
        InputEditor.TextArea.Caret.Position = new TextViewPosition(line, column);
        InputEditor.ScrollTo(line, column);
        InputEditor.Focus();
    }
}
