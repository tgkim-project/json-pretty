using System.Windows;

namespace JsonPretty.App;

/// <summary>언어·테마·출력·입력 설정. 바꾸는 즉시 메인 창에 적용된다.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LanguageCombo.Focus();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
