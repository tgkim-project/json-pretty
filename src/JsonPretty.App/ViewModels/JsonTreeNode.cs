using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using JsonPretty.App.Localization;
using JsonPretty.Core;

namespace JsonPretty.App.ViewModels;

public enum NodeKind { Object, Array, String, Number, Boolean, Null }

/// <summary>
/// 트리 보기의 노드 하나. 자식은 펼칠 때 만들어서 큰 문서도 빠르게 연다.
/// JsonElement는 JsonTreeModel이 가진 JsonDocument가 살아 있는 동안만 유효하다.
/// </summary>
public sealed partial class JsonTreeNode : ObservableObject
{
    private const int PreviewMaxLength = 120;
    private static readonly JsonTreeNode Placeholder = new();

    private readonly JsonElement _element;
    private bool _childrenLoaded;

    // 자식이 있다는 표시(펼침 화살표)만을 위한 자리 표시 노드
    private JsonTreeNode() { Name = string.Empty; Path = string.Empty; Preview = string.Empty; Kind = NodeKind.Null; }

    public JsonTreeNode(string name, string path, JsonElement element)
    {
        _element = element;
        Name = name;
        Path = path;
        Kind = element.ValueKind switch
        {
            JsonValueKind.Object => NodeKind.Object,
            JsonValueKind.Array => NodeKind.Array,
            JsonValueKind.String => NodeKind.String,
            JsonValueKind.Number => NodeKind.Number,
            JsonValueKind.True or JsonValueKind.False => NodeKind.Boolean,
            _ => NodeKind.Null,
        };
        Preview = Kind switch
        {
            NodeKind.Object => Loc.F("Tree_Object", element.EnumerateObject().Count()),
            NodeKind.Array => Loc.F("Tree_Array", element.GetArrayLength()),
            NodeKind.String => Truncate("\"" + element.GetString() + "\""),
            _ => element.GetRawText(),
        };

        if (HasChildren)
            Children.Add(Placeholder);
    }

    public string Name { get; }
    public string Path { get; }
    public NodeKind Kind { get; }
    public string Preview { get; }
    public bool HasChildren => Kind is NodeKind.Object or NodeKind.Array && Count > 0;
    private int Count => Kind == NodeKind.Object ? _element.EnumerateObject().Count() : _element.GetArrayLength();

    public ObservableCollection<JsonTreeNode> Children { get; } = [];

    [ObservableProperty] private bool isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (value) LoadChildren();
    }

    /// <summary>복사용 값: 문자열은 따옴표 없는 내용, 그 밖에는 JSON 원문.</summary>
    public string GetCopyValue(IJsonFormatter formatter, FormatOptions options) => Kind switch
    {
        NodeKind.String => _element.GetString()!,
        NodeKind.Object or NodeKind.Array => formatter.Format(_element.GetRawText(), options).Output,
        _ => _element.GetRawText(),
    };

    private void LoadChildren()
    {
        if (_childrenLoaded) return;
        _childrenLoaded = true;
        Children.Clear();

        if (Kind == NodeKind.Object)
        {
            foreach (var p in _element.EnumerateObject())
                Children.Add(new JsonTreeNode(p.Name, JsonPath.Property(Path, p.Name), p.Value));
        }
        else if (Kind == NodeKind.Array)
        {
            int i = 0;
            foreach (var item in _element.EnumerateArray())
            {
                Children.Add(new JsonTreeNode($"[{i}]", JsonPath.Index(Path, i), item));
                i++;
            }
        }
    }

    private static string Truncate(string s) =>
        s.Length <= PreviewMaxLength ? s.Replace("\r", "\\r").Replace("\n", "\\n")
                                     : s[..PreviewMaxLength].Replace("\r", "\\r").Replace("\n", "\\n") + "…";
}

/// <summary>트리 보기가 쓰는 JsonDocument를 들고 있다가, 새로 만들 때 이전 것을 정리한다.</summary>
public sealed class JsonTreeModel : IDisposable
{
    private JsonDocument? _document;

    public ObservableCollection<JsonTreeNode> Roots { get; } = [];

    public void Build(string json)
    {
        Roots.Clear();
        _document?.Dispose();
        _document = null;

        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            _document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = JsonFormatter.MaxDepth });
        }
        catch (JsonException)
        {
            return; // 출력은 항상 유효한 JSON이므로 여기 올 일은 거의 없다
        }

        var root = new JsonTreeNode("$", JsonPath.Root, _document.RootElement);
        Roots.Add(root);
        root.IsExpanded = true;
    }

    public void Dispose()
    {
        Roots.Clear();
        _document?.Dispose();
    }
}
