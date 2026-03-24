using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using DnDOverlay.Infrastructure;

namespace DnDOverlay.Theming
{
    public static class ThemeManager
    {
        private const string ThemeSettingsFileName = "theme.json";
        private static readonly string ThemeSettingsPath = AppPaths.GetDataFilePath(ThemeSettingsFileName);

        private static readonly IReadOnlyList<ThemeDefinition> _themes = new List<ThemeDefinition>
        {
            new(
                "dark",
                "Тёмная",
                (Color)ColorConverter.ConvertFromString("#FF111827"),
                (Color)ColorConverter.ConvertFromString("#FF1F2937"),
                (Color)ColorConverter.ConvertFromString("#FF0F172A"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FF1F2937"),
                (Color)ColorConverter.ConvertFromString("#FF334155"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FF263246"),
                (Color)ColorConverter.ConvertFromString("#FF3F4D63"),
                (Color)ColorConverter.ConvertFromString("#FF1E293B"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FF182031"),
                (Color)ColorConverter.ConvertFromString("#FF334155"),
                (Color)ColorConverter.ConvertFromString("#FF2B3445"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FF198754"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FFB78103"),
                (Color)ColorConverter.ConvertFromString("#FF1E1E1E"),
                (Color)ColorConverter.ConvertFromString("#FFB42318"),
                Colors.White),

            new(
                "light",
                "Светлая",
                (Color)ColorConverter.ConvertFromString("#FFF7F9FC"),
                (Color)ColorConverter.ConvertFromString("#FFCBD5E1"),
                (Color)ColorConverter.ConvertFromString("#FFE2E8F0"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFD9E0EC"),
                (Color)ColorConverter.ConvertFromString("#FFB0BEC5"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFEFF4FB"),
                (Color)ColorConverter.ConvertFromString("#FFC3CFDD"),
                (Color)ColorConverter.ConvertFromString("#FFFDFEFF"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFF0F4F8"),
                (Color)ColorConverter.ConvertFromString("#FFE2E8F0"),
                Colors.White,
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FF198754"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FFB78103"),
                (Color)ColorConverter.ConvertFromString("#FF1E1E1E"),
                (Color)ColorConverter.ConvertFromString("#FFB42318"),
                Colors.White),

            new(
                "dracula",
                "Dracula",
                (Color)ColorConverter.ConvertFromString("#FF282A36"),
                (Color)ColorConverter.ConvertFromString("#FF44475A"),
                (Color)ColorConverter.ConvertFromString("#FF21222C"),
                (Color)ColorConverter.ConvertFromString("#FFF8F8F2"),
                (Color)ColorConverter.ConvertFromString("#FF44475A"),
                (Color)ColorConverter.ConvertFromString("#FF6272A4"),
                (Color)ColorConverter.ConvertFromString("#FFF8F8F2"),
                (Color)ColorConverter.ConvertFromString("#FF343746"),
                (Color)ColorConverter.ConvertFromString("#FF44475A"),
                (Color)ColorConverter.ConvertFromString("#FF282A36"),
                (Color)ColorConverter.ConvertFromString("#FFF8F8F2"),
                (Color)ColorConverter.ConvertFromString("#FF191A21"),
                (Color)ColorConverter.ConvertFromString("#FF44475A"),
                (Color)ColorConverter.ConvertFromString("#FF282A36"),
                (Color)ColorConverter.ConvertFromString("#FFF8F8F2"),
                (Color)ColorConverter.ConvertFromString("#FF50FA7B"),
                (Color)ColorConverter.ConvertFromString("#FF282A36"),
                (Color)ColorConverter.ConvertFromString("#FFF1FA8C"),
                (Color)ColorConverter.ConvertFromString("#FF282A36"),
                (Color)ColorConverter.ConvertFromString("#FFFF5555"),
                (Color)ColorConverter.ConvertFromString("#FFF8F8F2")),

            new(
                "solarized",
                "Solarized Dark",
                (Color)ColorConverter.ConvertFromString("#FF002B36"),
                (Color)ColorConverter.ConvertFromString("#FF073642"),
                (Color)ColorConverter.ConvertFromString("#FF00212B"),
                (Color)ColorConverter.ConvertFromString("#FF839496"),
                (Color)ColorConverter.ConvertFromString("#FF073642"),
                (Color)ColorConverter.ConvertFromString("#FF586E75"),
                (Color)ColorConverter.ConvertFromString("#FF93A1A1"),
                (Color)ColorConverter.ConvertFromString("#FF003847"),
                (Color)ColorConverter.ConvertFromString("#FF586E75"),
                (Color)ColorConverter.ConvertFromString("#FF002B36"),
                (Color)ColorConverter.ConvertFromString("#FF839496"),
                (Color)ColorConverter.ConvertFromString("#FF001E26"),
                (Color)ColorConverter.ConvertFromString("#FF073642"),
                (Color)ColorConverter.ConvertFromString("#FF002B36"),
                (Color)ColorConverter.ConvertFromString("#FF93A1A1"),
                (Color)ColorConverter.ConvertFromString("#FF859900"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FFB58900"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFDC322F"),
                Colors.White),

            new(
                "forest",
                "Лесная",
                (Color)ColorConverter.ConvertFromString("#FF1A2F1A"),
                (Color)ColorConverter.ConvertFromString("#FF2D442D"),
                (Color)ColorConverter.ConvertFromString("#FF0F1F0F"),
                (Color)ColorConverter.ConvertFromString("#FFD0E0D0"),
                (Color)ColorConverter.ConvertFromString("#FF2D442D"),
                (Color)ColorConverter.ConvertFromString("#FF405540"),
                (Color)ColorConverter.ConvertFromString("#FFD0E0D0"),
                (Color)ColorConverter.ConvertFromString("#FF223822"),
                (Color)ColorConverter.ConvertFromString("#FF354F35"),
                (Color)ColorConverter.ConvertFromString("#FF1A2F1A"),
                (Color)ColorConverter.ConvertFromString("#FFD0E0D0"),
                (Color)ColorConverter.ConvertFromString("#FF142414"),
                (Color)ColorConverter.ConvertFromString("#FF2D442D"),
                (Color)ColorConverter.ConvertFromString("#FF1A2F1A"),
                (Color)ColorConverter.ConvertFromString("#FFD0E0D0"),
                (Color)ColorConverter.ConvertFromString("#FF4CAF50"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FFFFC107"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFF44336"),
                Colors.White),

            new(
                "cyberpunk",
                "Cyberpunk",
                (Color)ColorConverter.ConvertFromString("#FF050505"),
                (Color)ColorConverter.ConvertFromString("#FF1A1A1A"),
                (Color)ColorConverter.ConvertFromString("#FF000000"),
                (Color)ColorConverter.ConvertFromString("#FF00FF9F"),
                (Color)ColorConverter.ConvertFromString("#FF1A1A1A"),
                (Color)ColorConverter.ConvertFromString("#FF333333"),
                (Color)ColorConverter.ConvertFromString("#FF00FF9F"),
                (Color)ColorConverter.ConvertFromString("#FF0D0D0D"),
                (Color)ColorConverter.ConvertFromString("#FF262626"),
                (Color)ColorConverter.ConvertFromString("#FF050505"),
                (Color)ColorConverter.ConvertFromString("#FF00FF9F"),
                (Color)ColorConverter.ConvertFromString("#FF000000"),
                (Color)ColorConverter.ConvertFromString("#FF1A1A1A"),
                (Color)ColorConverter.ConvertFromString("#FF050505"),
                (Color)ColorConverter.ConvertFromString("#FF00FF9F"),
                (Color)ColorConverter.ConvertFromString("#FF00FF00"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFFF00FF"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFFF0000"),
                Colors.White),

            new(
                "nord",
                "Nord",
                (Color)ColorConverter.ConvertFromString("#FF3B4252"),
                (Color)ColorConverter.ConvertFromString("#FF2E3440"),
                (Color)ColorConverter.ConvertFromString("#FF2E3440"),
                (Color)ColorConverter.ConvertFromString("#FFECEFF4"),
                (Color)ColorConverter.ConvertFromString("#FF434C5E"),
                (Color)ColorConverter.ConvertFromString("#FF4C566A"),
                (Color)ColorConverter.ConvertFromString("#FFECEFF4"),
                (Color)ColorConverter.ConvertFromString("#FF4C566A"),
                (Color)ColorConverter.ConvertFromString("#FF5E81AC"),
                (Color)ColorConverter.ConvertFromString("#FF343C4A"),
                (Color)ColorConverter.ConvertFromString("#FFECEFF4"),
                (Color)ColorConverter.ConvertFromString("#FF2B303B"),
                (Color)ColorConverter.ConvertFromString("#FF4C566A"),
                (Color)ColorConverter.ConvertFromString("#FF3B4252"),
                (Color)ColorConverter.ConvertFromString("#FFECEFF4"),
                (Color)ColorConverter.ConvertFromString("#FF43A047"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FFF2C94C"),
                (Color)ColorConverter.ConvertFromString("#FF1F2933"),
                (Color)ColorConverter.ConvertFromString("#FFE53935"),
                Colors.White),

            new(
                "xp",
                "Голубая (XP)",
                (Color)ColorConverter.ConvertFromString("#FF3C6EF2"),
                (Color)ColorConverter.ConvertFromString("#FF1B4DBC"),
                (Color)ColorConverter.ConvertFromString("#FF1B4DBC"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FF6394FF"),
                (Color)ColorConverter.ConvertFromString("#FF0A2E8A"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FF7FA8FF"),
                (Color)ColorConverter.ConvertFromString("#FF517CE3"),
                (Color)ColorConverter.ConvertFromString("#FF4B7BFF"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FF3559C4"),
                (Color)ColorConverter.ConvertFromString("#FF0A2E8A"),
                (Color)ColorConverter.ConvertFromString("#FF5E8FFF"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FF1FAD7B"),
                Colors.White,
                (Color)ColorConverter.ConvertFromString("#FFFFD670"),
                (Color)ColorConverter.ConvertFromString("#FF1B365D"),
                (Color)ColorConverter.ConvertFromString("#FFFA5252"),
                Colors.White),

            new(
                "pink",
                "Розовая",
                (Color)ColorConverter.ConvertFromString("#FFFFB3D9"),
                (Color)ColorConverter.ConvertFromString("#FFE75480"),
                (Color)ColorConverter.ConvertFromString("#FFE75480"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFFF80BF"),
                (Color)ColorConverter.ConvertFromString("#FFCC4C7E"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFFF9FCE"),
                (Color)ColorConverter.ConvertFromString("#FFE064A3"),
                (Color)ColorConverter.ConvertFromString("#FFFFC2E5"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFFAB7DC"),
                (Color)ColorConverter.ConvertFromString("#FFE064A3"),
                (Color)ColorConverter.ConvertFromString("#FFFFD6EB"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFCC3F8C"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFFFD86A"),
                Colors.Black,
                (Color)ColorConverter.ConvertFromString("#FFFB4A5D"),
                Colors.Black)
        };

        public static IReadOnlyList<ThemeDefinition> Themes => _themes;
        public static ThemeDefinition DefaultTheme => _themes[0];
        public static ThemeDefinition CurrentTheme { get; private set; } = _themes[0];

        public static void Initialize()
        {
            var savedKey = LoadThemeKey();
            ApplyTheme(savedKey ?? DefaultTheme.Key, persist: false);
        }

        public static void ApplyTheme(string key) => ApplyTheme(key, persist: true);

        private static void ApplyTheme(string key, bool persist)
        {
            var theme = _themes.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))
                        ?? DefaultTheme;

            CurrentTheme = theme;
            ApplyThemeToResources(theme);
            AccentColorManager.RefreshResources();

            if (persist)
            {
                SaveThemeKey(theme.Key);
            }
        }

        private static void ApplyThemeToResources(ThemeDefinition theme)
        {
            var app = Application.Current;
            if (app == null)
            {
                return;
            }

            UpdateBrush(app, "AppWindowBackgroundBrush", theme.AppWindowBackground);
            UpdateBrush(app, "AppWindowBorderBrush", theme.AppWindowBorder);
            UpdateBrush(app, "HeaderBackgroundBrush", theme.HeaderBackground);
            UpdateBrush(app, "HeaderTextBrush", theme.HeaderText);
            UpdateBrush(app, "ControlButtonBackgroundBrush", theme.ControlButtonBackground);
            UpdateBrush(app, "ControlButtonBorderBrush", theme.ControlButtonBorder);
            UpdateBrush(app, "ControlButtonForegroundBrush", theme.ControlButtonForeground);
            UpdateBrush(app, "ControlButtonHoverBrush", theme.ControlButtonHoverBackground);
            UpdateBrush(app, "ControlButtonPressedBrush", theme.ControlButtonPressedBackground);
            UpdateBrush(app, "SurfaceBackgroundBrush", theme.SurfaceBackground);
            UpdateBrush(app, "SurfaceForegroundBrush", theme.SurfaceForeground);
            UpdateBrush(app, "PanelBackgroundBrush", theme.PanelBackground);
            UpdateBrush(app, "PanelBorderBrush", theme.PanelBorder);
            UpdateBrush(app, "InputBackgroundBrush", theme.InputBackground);
            UpdateBrush(app, "InputForegroundBrush", theme.InputForeground);
            UpdateBrush(app, "StatusGoodBackgroundBrush", theme.StatusGoodBackground);
            UpdateBrush(app, "StatusGoodForegroundBrush", theme.StatusGoodForeground);
            UpdateBrush(app, "StatusWarningBackgroundBrush", theme.StatusWarningBackground);
            UpdateBrush(app, "StatusWarningForegroundBrush", theme.StatusWarningForeground);
            UpdateBrush(app, "StatusDangerBackgroundBrush", theme.StatusDangerBackground);
            UpdateBrush(app, "StatusDangerForegroundBrush", theme.StatusDangerForeground);
        }

        private static void UpdateBrush(Application app, string resourceKey, Color color)
        {
            app.Resources[resourceKey] = new SolidColorBrush(color);
        }

        private static void SaveThemeKey(string key)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemeSettingsPath)!);
                File.WriteAllText(ThemeSettingsPath, JsonSerializer.Serialize(new ThemeSettings { ThemeKey = key }));
            }
            catch
            {
                // ignore persistence failures
            }
        }

        private static string? LoadThemeKey()
        {
            try
            {
                if (File.Exists(ThemeSettingsPath))
                {
                    var json = File.ReadAllText(ThemeSettingsPath);
                    var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
                    return settings?.ThemeKey;
                }
            }
            catch
            {
                // ignore and fallback to default
            }

            return null;
        }

        private class ThemeSettings
        {
            public string? ThemeKey { get; set; }
        }
    }
}
