using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace JsonPretty.App.Editor;

/// <summary>
/// 입력 에디터에서 파싱 오류가 난 줄을 연한 빨강으로 칠하고,
/// 오류 글자 아래에 빨간 밑줄을 그린다.
/// </summary>
public sealed class ErrorMarkerRenderer : IBackgroundRenderer
{
    private static readonly Brush LineBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x60, 0x60)));
    private static readonly Pen UnderlinePen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C)), 2));

    private readonly TextView _textView;
    private int? _line;
    private int _column;

    public ErrorMarkerRenderer(TextView textView)
    {
        _textView = textView;
        _textView.BackgroundRenderers.Add(this);
    }

    public KnownLayer Layer => KnownLayer.Background;

    /// <summary>오류 위치 설정. line이 null이면 표시를 지운다.</summary>
    public void SetError(int? line, int column)
    {
        if (_line == line && _column == column) return;
        _line = line;
        _column = column;
        _textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_line is not int lineNumber || textView.Document is not { } doc) return;
        if (lineNumber < 1 || lineNumber > doc.LineCount) return;

        textView.EnsureVisualLines();
        DocumentLine line = doc.GetLineByNumber(lineNumber);

        // 줄 전체 배경
        var lineSegment = new TextSegment { StartOffset = line.Offset, EndOffset = line.EndOffset };
        foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, lineSegment, extendToFullWidthAtLineEnd: true))
            drawingContext.DrawRectangle(LineBrush, null, new Rect(0, r.Top, Math.Max(textView.ActualWidth, r.Right), r.Height));

        // 오류 글자 밑줄 (줄 끝을 넘으면 마지막 글자 자리)
        int col = Math.Clamp(_column, 1, Math.Max(1, line.Length));
        int start = line.Offset + col - 1;
        int end = Math.Min(start + 1, line.EndOffset);
        if (end <= start)
        {
            // 빈 줄: 줄 시작에 짧은 밑줄
            var pos = textView.GetVisualPosition(new ICSharpCode.AvalonEdit.TextViewPosition(lineNumber, 1), VisualYPosition.LineBottom);
            pos -= textView.ScrollOffset;
            drawingContext.DrawLine(UnderlinePen, new Point(pos.X, pos.Y - 1), new Point(pos.X + 8, pos.Y - 1));
            return;
        }

        var charSegment = new TextSegment { StartOffset = start, EndOffset = end };
        foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, charSegment))
            drawingContext.DrawLine(UnderlinePen, new Point(r.Left, r.Bottom - 1), new Point(Math.Max(r.Right, r.Left + 8), r.Bottom - 1));
    }

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
