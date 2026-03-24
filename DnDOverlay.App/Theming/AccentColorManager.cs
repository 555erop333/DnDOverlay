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
    public static class AccentColorManager
    {
        private const string AccentSettingsFileName = "accent.json";
        private static readonly string AccentSettingsPath = AppPaths.GetDataFilePath(AccentSettingsFileName);

        private static readonly IReadOnlyList<AccentColorDefinition> _accents = new List<AccentColorDefinition>
        {
            new("graphite", "Графит", (Color)ColorConverter.ConvertFromString("#FF64748B")),
            new("blue", "Синий", (Color)ColorConverter.ConvertFromString("#FF3A7AFE")),
            new("purple", "Фиолетовый", (Color)ColorConverter.ConvertFromString("#FF7C5BFF")),
            new("emerald", "Изумрудный", (Color)ColorConverter.ConvertFromString("#FF10B981")),
            new("orange", "Оранжевый", (Color)ColorConverter.ConvertFromString("#FFF59E0B")),
            new("red", "Красный", (Color)ColorConverter.ConvertFromString("#FFEF4444")),
            new("crimson", "Малиновый", (Color)ColorConverter.ConvertFromString("#FFE11D48")),
            new("amber", "Янтарный", (Color)ColorConverter.ConvertFromString("#FFD97706")),
            new("indigo", "Индиго", (Color)ColorConverter.ConvertFromString("#FF6366F1")),
            new("cyan", "Циан", (Color)ColorConverter.ConvertFromString("#FF06B6D4")),
            new("lime", "Лайм", (Color)ColorConverter.ConvertFromString("#FF84CC16")),
            new("fuchsia", "Фуксия", (Color)ColorConverter.ConvertFromString("#FFD946EF"))
        };

        public static IReadOnlyList<AccentColorDefinition> Accents => _accents;
        public static AccentColorDefinition DefaultAccent => _accents[0];
        public static AccentColorDefinition CurrentAccent { get; private set; } = _accents[0];

        public static void Initialize()
        {
            var savedKey = LoadAccentKey();
            ApplyAccent(savedKey ?? DefaultAccent.Key, persist: false);
        }

        public static void ApplyAccent(string key) => ApplyAccent(key, persist: true);

        internal static void RefreshResources() => ApplyAccentToResources(CurrentAccent);

        private static void ApplyAccent(string key, bool persist)
        {
            var accent = _accents.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))
                         ?? DefaultAccent;

            CurrentAccent = accent;
            ApplyAccentToResources(accent);

            if (persist)
            {
                SaveAccentKey(accent.Key);
            }
        }

        private static void ApplyAccentToResources(AccentColorDefinition accent)
        {
            var app = Application.Current;
            if (app == null)
            {
                return;
            }

            UpdateBrush(app, "AccentBrush", accent.PrimaryColor);
            UpdateBrush(app, "AccentForegroundBrush", accent.PrimaryForegroundColor);
            UpdateBrush(app, "AccentHoverBrush", accent.HoverColor);
            UpdateBrush(app, "AccentPressedBrush", accent.PressedColor);
            UpdateBrush(app, "AccentBorderBrush", accent.BorderColor);
            UpdateBrush(app, "AccentSubtleBrush", accent.SubtleBackgroundColor);

            UpdateBrush(app, "ControlButtonBackgroundBrush", accent.SubtleBackgroundColor);
            UpdateBrush(app, "ControlButtonBorderBrush", accent.BorderColor);
            // ControlButtonForegroundBrush is determined by the Theme, not the Accent
            UpdateBrush(app, "ControlButtonHoverBrush", accent.HoverColor);
            UpdateBrush(app, "ControlButtonPressedBrush", accent.PressedColor);
        }

        private static void UpdateBrush(Application app, string resourceKey, Color color)
        {
            app.Resources[resourceKey] = new SolidColorBrush(color);
        }

        private static void SaveAccentKey(string key)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AccentSettingsPath)!);
                File.WriteAllText(AccentSettingsPath, JsonSerializer.Serialize(new AccentSettings { AccentKey = key }));
            }
            catch
            {
                // ignore persistence failures
            }
        }

        private static string? LoadAccentKey()
        {
            try
            {
                if (File.Exists(AccentSettingsPath))
                {
                    var json = File.ReadAllText(AccentSettingsPath);
                    var settings = JsonSerializer.Deserialize<AccentSettings>(json);
                    return settings?.AccentKey;
                }
            }
            catch
            {
                // ignore and fallback to default
            }

            return null;
        }

        private class AccentSettings
        {
            public string? AccentKey { get; set; }
        }
    }
}
