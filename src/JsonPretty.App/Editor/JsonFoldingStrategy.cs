using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using JsonPretty.App.Localization;
using JsonPretty.Core;

namespace JsonPretty.App.Editor;

/// <summary>FoldScanner 결과를 AvalonEdit 접기 정보로 바꾼다.</summary>
public static class JsonFoldingStrategy
{
    public static void Update(FoldingManager manager, TextDocument document)
    {
        var folds = FoldScanner.Scan(document.Text)
            .Select(f => new NewFolding(f.Start, f.End)
            {
                Name = f.Open == '{'
                    ? Loc.F("Fold_Object", f.ChildCount)
                    : Loc.F("Fold_Array", f.ChildCount),
            });

        manager.UpdateFoldings(folds, -1);
    }

    public static void SetAll(FoldingManager manager, bool folded)
    {
        foreach (var section in manager.AllFoldings)
            section.IsFolded = folded;
    }
}
