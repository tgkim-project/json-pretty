using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using JsonPretty.App.Editor;
using JsonPretty.App.ViewModels;

namespace JsonPretty.App;

public partial class MainWindow : Window
{
    private readonly FoldingManager _outputFolding;
    private readonly ErrorMarkerRenderer _errorMarker;

    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        var highlighting = HighlightingManager.Instance.GetDefinition("Json")
                           ?? HighlightingManager.Instance.GetDefinition("JavaScript");
        InputEditor.SyntaxHighlighting = highlighting;
        OutputEditor.SyntaxHighlighting = highlighting;

        _outputFolding = FoldingManager.Install(OutputEditor.TextArea);
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

        Loaded += (_, _) => InputEditor.Focus();
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
