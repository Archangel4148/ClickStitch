using System.Windows;
using System.Windows.Media;
using ClickStitch.Plugins;
using Microsoft.Win32;

namespace ClickStitch;

/// <summary>Applies the light, dark, or high-contrast palette to the shell brushes.</summary>
public static class ShellTheme
{
    public static void Apply(Application application, AppAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (SystemParameters.HighContrast)
        {
            Set(
                application,
                SystemColors.WindowColor,
                SystemColors.WindowTextColor,
                SystemColors.HighlightColor,
                SystemColors.HighlightColor,
                SystemColors.HighlightTextColor,
                SystemColors.WindowColor,
                SystemColors.WindowTextColor,
                SystemColors.WindowColor,
                SystemColors.WindowTextColor,
                SystemColors.HighlightTextColor);
            application.Resources["StartBrush"] = new SolidColorBrush(SystemColors.HighlightColor);
            return;
        }

        bool dark = appearance switch
        {
            AppAppearance.Dark => true,
            AppAppearance.Light => false,
            _ => !WindowsAppsUseLightTheme(),
        };

        if (dark)
        {
            Set(
                application,
                Color(0x0E, 0x13, 0x24),
                Color(0xF4, 0xF1, 0xFB),
                Color(0xD2, 0xB4, 0xFF),
                Color(0xFF, 0xB4, 0xC6),
                Color(0x2A, 0x0E, 0x18),
                Color(0x1A, 0x22, 0x38),
                Color(0xC9, 0xC0, 0xDC),
                Color(0x0E, 0x13, 0x24),
                Color(0x3E, 0x46, 0x66),
                Color(0x1A, 0x10, 0x30));
        }
        else
        {

        Set(
            application,
            Color(0xF7, 0xF4, 0xFC),
            Color(0x02, 0x07, 0x18),
            Color(0x5E, 0x35, 0xC4),
            Color(0xA6, 0x1E, 0x45),
            Color(0xFF, 0xFF, 0xFF),
            Color(0xFF, 0xFF, 0xFF),
            Color(0x3C, 0x35, 0x54),
            Color(0xFF, 0xFF, 0xFF),
            Color(0xD5, 0xC8, 0xEA),
            Color(0xFF, 0xFF, 0xFF));
        }

        Shade(application, dark);
    }

    private static bool WindowsAppsUseLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (System.Security.SecurityException)
        {
            return true;
        }
        catch (System.IO.IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void Set(
        Application application,
        Color paper,
        Color ink,
        Color thread,
        Color stop,
        Color stopText,
        Color card,
        Color muted,
        Color field,
        Color line,
        Color accentText)
    {
        application.Resources["PaperBrush"] = new SolidColorBrush(paper);
        application.Resources["InkBrush"] = new SolidColorBrush(ink);
        application.Resources["ThreadBrush"] = new SolidColorBrush(thread);
        application.Resources["StopBrush"] = new SolidColorBrush(stop);
        application.Resources["StopTextBrush"] = new SolidColorBrush(stopText);
        application.Resources["CardBrush"] = new SolidColorBrush(card);
        application.Resources["MutedBrush"] = new SolidColorBrush(muted);
        application.Resources["FieldBrush"] = new SolidColorBrush(field);
        application.Resources["LineBrush"] = new SolidColorBrush(line);
        application.Resources["AccentTextBrush"] = new SolidColorBrush(accentText);
        application.Resources[SystemColors.WindowBrushKey] = new SolidColorBrush(field);
        application.Resources[SystemColors.WindowTextBrushKey] = new SolidColorBrush(ink);
        application.Resources[SystemColors.ControlBrushKey] = new SolidColorBrush(card);
        application.Resources[SystemColors.ControlTextBrushKey] = new SolidColorBrush(ink);
    }

    private static void Shade(Application application, bool dark)
    {
        if (dark)
        {
            application.Resources["PaperBrush"] = new LinearGradientBrush(Color(0x0B, 0x10, 0x20), Color(0x16, 0x12, 0x2C), 90);
            application.Resources["StartBrush"] = new LinearGradientBrush(Color(0xED, 0xE9, 0xFE), Color(0xC4, 0xB5, 0xFD), 0);
            return;
        }

        application.Resources["PaperBrush"] = new LinearGradientBrush(Color(0xF7, 0xF5, 0xFC), Color(0xE6, 0xDE, 0xF4), 90);
        application.Resources["StartBrush"] = new LinearGradientBrush(Color(0x5B, 0x21, 0xB6), Color(0x7C, 0x3A, 0xED), 0);
    }

    private static Color Color(byte red, byte green, byte blue) => System.Windows.Media.Color.FromRgb(red, green, blue);
}
