using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace JsonPretty.App.Localization;

public enum AppLanguage { System, Korean, English }

/// <summary>
/// 현재 언어의 문구를 돌려준다. 언어를 바꾸면 XAML 바인딩(Item[])과 LanguageChanged 구독자가 즉시 갱신된다.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private IReadOnlyDictionary<string, string> _table = Strings.En;

    private Loc() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>문구를 다시 만들어야 하는 곳(상태바, 트리, 접기 이름 등)을 위한 알림.</summary>
    public event EventHandler? LanguageChanged;

    public bool IsKorean { get; private set; }

    public string this[string key] =>
        _table.TryGetValue(key, out var v) ? v :
        Strings.En.TryGetValue(key, out var en) ? en : key;

    public static string T(string key) => Instance[key];

    public static string F(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Instance[key], args);

    public void SetLanguage(AppLanguage language)
    {
        bool korean = language switch
        {
            AppLanguage.Korean => true,
            AppLanguage.English => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko",
        };

        IsKorean = korean;
        _table = korean ? Strings.Ko : Strings.En;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsKorean)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// XAML에서 <c>Content="{l:Tr Open}"</c> 처럼 쓰는 번역 바인딩. 언어를 바꾸면 자동으로 다시 그려진다.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}

/// <summary>콤보박스 선택지처럼, 언어가 바뀌면 표시 이름이 바뀌어야 하는 항목의 기반 클래스.</summary>
public abstract class LocalizedItem : INotifyPropertyChanged
{
    private readonly string _labelKey;

    protected LocalizedItem(string labelKey)
    {
        _labelKey = labelKey;
        Loc.Instance.LanguageChanged += (_, _) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
    }

    public string Label => Loc.T(_labelKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => Label;
}
