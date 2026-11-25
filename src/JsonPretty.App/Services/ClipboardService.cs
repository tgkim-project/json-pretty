using System.Runtime.InteropServices;
using System.Windows;

namespace JsonPretty.App.Services;

/// <summary>
/// 다른 프로그램이 클립보드를 잡고 있으면 CLIPBRD_E_CANT_OPEN이 나므로 짧게 재시도한다.
/// </summary>
public static class ClipboardService
{
    private const int Retries = 5;
    private const int DelayMs = 50;

    public static bool TrySetText(string text)
    {
        for (int i = 0; i < Retries; i++)
        {
            try
            {
                // SetDataObject(copy: true)는 프로그램이 종료돼도 클립보드 내용이 남는다.
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(DelayMs);
            }
        }
        return false;
    }

    public static string? TryGetText()
    {
        for (int i = 0; i < Retries; i++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (COMException)
            {
                Thread.Sleep(DelayMs);
            }
        }
        return null;
    }
}
