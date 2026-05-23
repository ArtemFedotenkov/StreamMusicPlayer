using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using StreamMusicPlayer.Models;

namespace StreamMusicPlayer.Services;

public static class ThemeService
{
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private static Color currentTitleBarBackground = Color.FromRgb(0xF4, 0xF6, 0xF8);
    private static Color currentTitleBarText = Color.FromRgb(0x1F, 0x29, 0x33);
    private static Color currentTitleBarBorder = Color.FromRgb(0xD7, 0xDE, 0xE7);

    public static void Apply(AppTheme theme)
    {
        var colors = theme switch
        {
            AppTheme.Dark => DarkColors,
            AppTheme.Cyberpunk => CyberpunkColors,
            AppTheme.Olive => OliveColors,
            AppTheme.MidnightBlue => MidnightBlueColors,
            AppTheme.DarkRed => DarkRedColors,
            _ => LightColors
        };
        foreach (var (resourceKey, color) in colors)
        {
            SetBrush(resourceKey, color);
        }

        currentTitleBarBackground = colors["PanelBackgroundBrush"];
        currentTitleBarText = colors["PrimaryTextBrush"];
        currentTitleBarBorder = colors["PanelBorderBrush"];
        RefreshOpenWindows();
    }

    public static void ApplyToWindow(Window window)
    {
        if (window.IsLoaded)
        {
            ApplyTitleBarColors(window);
            return;
        }

        window.SourceInitialized += (_, _) => ApplyTitleBarColors(window);
    }

    private static readonly Dictionary<string, Color> LightColors = new()
    {
        ["WindowBackgroundBrush"] = Color.FromRgb(0xF4, 0xF6, 0xF8),
        ["PanelBackgroundBrush"] = Colors.White,
        ["PanelAltBackgroundBrush"] = Color.FromRgb(0xF8, 0xFA, 0xFC),
        ["PanelBorderBrush"] = Color.FromRgb(0xD7, 0xDE, 0xE7),
        ["PrimaryTextBrush"] = Color.FromRgb(0x1F, 0x29, 0x33),
        ["MutedTextBrush"] = Color.FromRgb(0x65, 0x73, 0x86),
        ["AccentBrush"] = Color.FromRgb(0x25, 0x63, 0xEB),
        ["AccentSoftBrush"] = Color.FromRgb(0xEA, 0xF1, 0xFF),
        ["DangerBrush"] = Color.FromRgb(0xB4, 0x23, 0x18),
        ["PressedBrush"] = Color.FromRgb(0xD9, 0xE7, 0xFF),
        ["DisabledBackgroundBrush"] = Color.FromRgb(0xF2, 0xF4, 0xF7),
        ["DisabledBorderBrush"] = Color.FromRgb(0xE1, 0xE7, 0xEF),
        ["DisabledTextBrush"] = Color.FromRgb(0x9A, 0xA5, 0xB1),
        ["SelectionBorderBrush"] = Color.FromRgb(0xC7, 0xDA, 0xFF)
    };

    private static readonly Dictionary<string, Color> DarkColors = new()
    {
        ["WindowBackgroundBrush"] = Color.FromRgb(0x10, 0x14, 0x18),
        ["PanelBackgroundBrush"] = Color.FromRgb(0x17, 0x1D, 0x23),
        ["PanelAltBackgroundBrush"] = Color.FromRgb(0x20, 0x28, 0x32),
        ["PanelBorderBrush"] = Color.FromRgb(0x34, 0x43, 0x52),
        ["PrimaryTextBrush"] = Color.FromRgb(0xF3, 0xF6, 0xF8),
        ["MutedTextBrush"] = Color.FromRgb(0xA5, 0xB2, 0xBE),
        ["AccentBrush"] = Color.FromRgb(0x5B, 0xA7, 0xFF),
        ["AccentSoftBrush"] = Color.FromRgb(0x21, 0x36, 0x4D),
        ["DangerBrush"] = Color.FromRgb(0xF9, 0x70, 0x66),
        ["PressedBrush"] = Color.FromRgb(0x2A, 0x49, 0x68),
        ["DisabledBackgroundBrush"] = Color.FromRgb(0x14, 0x19, 0x1E),
        ["DisabledBorderBrush"] = Color.FromRgb(0x28, 0x33, 0x3E),
        ["DisabledTextBrush"] = Color.FromRgb(0x6F, 0x7C, 0x88),
        ["SelectionBorderBrush"] = Color.FromRgb(0x3F, 0x64, 0x8F)
    };

    private static readonly Dictionary<string, Color> CyberpunkColors = new()
    {
        ["WindowBackgroundBrush"] = Color.FromRgb(0x0B, 0x0E, 0x18),
        ["PanelBackgroundBrush"] = Color.FromRgb(0x12, 0x16, 0x24),
        ["PanelAltBackgroundBrush"] = Color.FromRgb(0x1C, 0x1B, 0x31),
        ["PanelBorderBrush"] = Color.FromRgb(0x3D, 0xF7, 0xFF),
        ["PrimaryTextBrush"] = Color.FromRgb(0xF5, 0xF7, 0xFF),
        ["MutedTextBrush"] = Color.FromRgb(0x9F, 0xA8, 0xC6),
        ["AccentBrush"] = Color.FromRgb(0xFF, 0x2E, 0xD8),
        ["AccentSoftBrush"] = Color.FromRgb(0x2E, 0x1E, 0x45),
        ["DangerBrush"] = Color.FromRgb(0xFF, 0x54, 0x6D),
        ["PressedBrush"] = Color.FromRgb(0x32, 0x2C, 0x62),
        ["DisabledBackgroundBrush"] = Color.FromRgb(0x12, 0x14, 0x1E),
        ["DisabledBorderBrush"] = Color.FromRgb(0x2A, 0x30, 0x46),
        ["DisabledTextBrush"] = Color.FromRgb(0x68, 0x70, 0x8E),
        ["SelectionBorderBrush"] = Color.FromRgb(0xFF, 0xD8, 0x3D)
    };

    private static readonly Dictionary<string, Color> OliveColors = new()
    {
        ["WindowBackgroundBrush"] = Color.FromRgb(0x18, 0x1C, 0x13),
        ["PanelBackgroundBrush"] = Color.FromRgb(0x20, 0x27, 0x18),
        ["PanelAltBackgroundBrush"] = Color.FromRgb(0x2A, 0x32, 0x20),
        ["PanelBorderBrush"] = Color.FromRgb(0x55, 0x66, 0x3C),
        ["PrimaryTextBrush"] = Color.FromRgb(0xF1, 0xF3, 0xE8),
        ["MutedTextBrush"] = Color.FromRgb(0xB3, 0xBB, 0x9A),
        ["AccentBrush"] = Color.FromRgb(0xB9, 0xD7, 0x57),
        ["AccentSoftBrush"] = Color.FromRgb(0x35, 0x43, 0x22),
        ["DangerBrush"] = Color.FromRgb(0xF0, 0x84, 0x63),
        ["PressedBrush"] = Color.FromRgb(0x45, 0x55, 0x2C),
        ["DisabledBackgroundBrush"] = Color.FromRgb(0x18, 0x1D, 0x14),
        ["DisabledBorderBrush"] = Color.FromRgb(0x36, 0x40, 0x2A),
        ["DisabledTextBrush"] = Color.FromRgb(0x73, 0x7B, 0x62),
        ["SelectionBorderBrush"] = Color.FromRgb(0xD4, 0xE8, 0x8A)
    };

    private static readonly Dictionary<string, Color> MidnightBlueColors = new()
    {
        ["WindowBackgroundBrush"] = Color.FromRgb(0x0A, 0x12, 0x22),
        ["PanelBackgroundBrush"] = Color.FromRgb(0x10, 0x1B, 0x2F),
        ["PanelAltBackgroundBrush"] = Color.FromRgb(0x16, 0x25, 0x3D),
        ["PanelBorderBrush"] = Color.FromRgb(0x2E, 0x48, 0x6E),
        ["PrimaryTextBrush"] = Color.FromRgb(0xF0, 0xF6, 0xFF),
        ["MutedTextBrush"] = Color.FromRgb(0x99, 0xAC, 0xC8),
        ["AccentBrush"] = Color.FromRgb(0x6E, 0xB8, 0xFF),
        ["AccentSoftBrush"] = Color.FromRgb(0x1A, 0x35, 0x56),
        ["DangerBrush"] = Color.FromRgb(0xFF, 0x7A, 0x7A),
        ["PressedBrush"] = Color.FromRgb(0x21, 0x43, 0x69),
        ["DisabledBackgroundBrush"] = Color.FromRgb(0x0D, 0x17, 0x28),
        ["DisabledBorderBrush"] = Color.FromRgb(0x24, 0x35, 0x4E),
        ["DisabledTextBrush"] = Color.FromRgb(0x66, 0x77, 0x90),
        ["SelectionBorderBrush"] = Color.FromRgb(0x86, 0xC7, 0xFF)
    };

    private static readonly Dictionary<string, Color> DarkRedColors = new()
    {
        ["WindowBackgroundBrush"] = Color.FromRgb(0x18, 0x0D, 0x0F),
        ["PanelBackgroundBrush"] = Color.FromRgb(0x22, 0x13, 0x16),
        ["PanelAltBackgroundBrush"] = Color.FromRgb(0x31, 0x1A, 0x1F),
        ["PanelBorderBrush"] = Color.FromRgb(0x64, 0x34, 0x3D),
        ["PrimaryTextBrush"] = Color.FromRgb(0xFF, 0xF4, 0xF2),
        ["MutedTextBrush"] = Color.FromRgb(0xC8, 0xA0, 0xA0),
        ["AccentBrush"] = Color.FromRgb(0xFF, 0x70, 0x5F),
        ["AccentSoftBrush"] = Color.FromRgb(0x48, 0x21, 0x27),
        ["DangerBrush"] = Color.FromRgb(0xFF, 0xB0, 0x4A),
        ["PressedBrush"] = Color.FromRgb(0x5A, 0x28, 0x30),
        ["DisabledBackgroundBrush"] = Color.FromRgb(0x1A, 0x0F, 0x11),
        ["DisabledBorderBrush"] = Color.FromRgb(0x3B, 0x21, 0x26),
        ["DisabledTextBrush"] = Color.FromRgb(0x83, 0x65, 0x65),
        ["SelectionBorderBrush"] = Color.FromRgb(0xFF, 0x99, 0x8B)
    };

    private static void SetBrush(string resourceKey, Color color)
    {
        if (Application.Current.Resources[resourceKey] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        Application.Current.Resources[resourceKey] = new SolidColorBrush(color);
    }

    private static void RefreshOpenWindows()
    {
        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBarColors(window);
            window.InvalidateVisual();
            window.UpdateLayout();
        }
    }

    private static void ApplyTitleBarColors(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetDwmColor(handle, DwmwaCaptionColor, currentTitleBarBackground);
        SetDwmColor(handle, DwmwaTextColor, currentTitleBarText);
        SetDwmColor(handle, DwmwaBorderColor, currentTitleBarBorder);
    }

    private static void SetDwmColor(IntPtr handle, int attribute, Color color)
    {
        var colorRef = ToColorRef(color);
        _ = DwmSetWindowAttribute(handle, attribute, ref colorRef, sizeof(int));
    }

    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
