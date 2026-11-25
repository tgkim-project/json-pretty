using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;

namespace JsonPretty.App.Services;

public enum AppTheme { System, Light, Dark }

/// <summary>
/// 테마 전환. 컨트롤 모양은 WPF(.NET 9) Fluent 테마(ThemeMode)가, 앱 고유 색과
/// 에디터 구문 강조 색은 Themes/Light.xaml · Dark.xaml 을 바꿔 끼워서 맞춘다.
/// </summary>
public static class ThemeService
{
    private static AppTheme _current = AppTheme.System;
    private static ResourceDictionary? _appliedColors;

    /// <summary>테마가 실제로 바뀐 뒤(에디터 다시 그리기 등) 알림.</summary>
    public static event EventHandler? Applied;

    public static void Initialize(AppTheme theme)
    {
        // 시스템 설정(라이트/다크)이 바뀌면 "시스템" 모드에서 따라간다.
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _current == AppTheme.System)
                Application.Current.Dispatcher.BeginInvoke(() => Apply(AppTheme.System));
        };
        Apply(theme);
    }

    public static void Apply(AppTheme theme)
    {
        _current = theme;
        bool dark = theme == AppTheme.Dark || (theme == AppTheme.System && IsSystemDark());

        var app = Application.Current;
#pragma warning disable WPF0001 // ThemeMode는 .NET 9에서 실험 기능
        app.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
#pragma warning restore WPF0001

        var colors = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/JsonPretty;component/Themes/{(dark ? "Dark" : "Light")}.xaml"),
        };
        if (_appliedColors is not null)
            app.Resources.MergedDictionaries.Remove(_appliedColors);
        app.Resources.MergedDictionaries.Add(colors);
        _appliedColors = colors;

        ApplyHighlightingColors(colors);
        Applied?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Windows 설정 > 개인 설정 > 색 의 "앱 모드"가 다크인지.</summary>
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>
    /// AvalonEdit 기본 JSON 구문 강조의 색을 테마 색으로 바꾼다.
    /// 강조 정의는 전역으로 공유되므로 여기서 한 번 바꾸면 두 에디터 모두 반영된다.
    /// </summary>
    private static void ApplyHighlightingColors(ResourceDictionary colors)
    {
        var def = GetJsonHighlighting();
        if (def is null) return;

        foreach (var named in def.NamedHighlightingColors)
        {
            string name = named.Name.ToLowerInvariant();
            string? key =
                name.Contains("field") || name.Contains("key") || name.Contains("property") ? "Color.Key" :
                name.Contains("string") || name.Contains("char") ? "Color.String" :
                name.Contains("number") || name.Contains("digit") ? "Color.Number" :
                name.Contains("bool") || name.Contains("null") || name.Contains("keyword") || name.Contains("literal") ? "Color.Literal" :
                name.Contains("punct") || name.Contains("bracket") ? "Color.Punctuation" :
                null;
            if (key is not null && colors[key] is Color c)
                named.Foreground = new SimpleHighlightingBrush(c);
        }
    }

    public static IHighlightingDefinition? GetJsonHighlighting() =>
        HighlightingManager.Instance.GetDefinition("Json") ?? HighlightingManager.Instance.GetDefinition("JavaScript");
}
