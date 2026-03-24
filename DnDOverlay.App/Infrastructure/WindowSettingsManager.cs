using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace DnDOverlay.Infrastructure
{
    /// <summary>
    /// Класс для управления сохранением и загрузкой настроек окон
    /// </summary>
    public class WindowSettingsManager
    {
        private static readonly string SettingsFilePath =
            AppPaths.GetDataFilePath("window_settings.json");

        private static Dictionary<string, WindowSettings> _settings = new Dictionary<string, WindowSettings>();

        /// <summary>
        /// Настройки конкретного окна
        /// </summary>
        public class WindowSettings
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public bool IsTopmost { get; set; }
            public Dictionary<string, double> Doubles { get; set; } = new Dictionary<string, double>();
        }

        static WindowSettingsManager()
        {
            LoadSettings();
        }

        /// <summary>
        /// Загружает настройки из файла
        /// </summary>
        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    _settings = JsonSerializer.Deserialize<Dictionary<string, WindowSettings>>(json) 
                                ?? new Dictionary<string, WindowSettings>();
                }
            }
            catch
            {
                // Игнорируем ошибки загрузки, используем настройки по умолчанию
                _settings = new Dictionary<string, WindowSettings>();
            }
        }

        /// <summary>
        /// Сохраняет настройки в файл
        /// </summary>
        private static void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Игнорируем ошибки сохранения
            }
        }

        /// <summary>
        /// Применяет сохраненные настройки к окну при его открытии
        /// </summary>
        /// <param name="window">Окно для применения настроек</param>
        /// <param name="windowKey">Уникальный ключ окна (обычно имя класса)</param>
        public static void ApplySettings(Window window, string windowKey)
        {
            if (_settings.TryGetValue(windowKey, out var settings))
            {
                // Применяем позицию, если она валидна
                if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top))
                {
                    window.Left = settings.Left;
                    window.Top = settings.Top;
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                }

                // Применяем размеры, если они валидны
                if (!double.IsNaN(settings.Width) && settings.Width > 0)
                {
                    window.Width = settings.Width;
                }
                if (!double.IsNaN(settings.Height) && settings.Height > 0)
                {
                    window.Height = settings.Height;
                }

                // Применяем закрепленность
                window.Topmost = settings.IsTopmost;
            }
        }

        /// <summary>
        /// Сохраняет текущие настройки окна
        /// </summary>
        /// <param name="window">Окно для сохранения настроек</param>
        /// <param name="windowKey">Уникальный ключ окна (обычно имя класса)</param>
        public static void SaveWindowSettings(Window window, string windowKey)
        {
            if (!_settings.TryGetValue(windowKey, out var settings))
            {
                settings = new WindowSettings();
                _settings[windowKey] = settings;
            }

            settings.Left = window.Left;
            settings.Top = window.Top;
            settings.Width = window.ActualWidth;
            settings.Height = window.ActualHeight;
            settings.IsTopmost = window.Topmost;
            SaveSettings();
        }

        /// <summary>
        /// Регистрирует окно для автоматического сохранения настроек
        /// </summary>
        /// <param name="window">Окно для регистрации</param>
        /// <param name="windowKey">Уникальный ключ окна (обычно имя класса)</param>
        public static void RegisterWindow(Window window, string windowKey)
        {
            // Применяем сохраненные настройки сразу
            ApplySettings(window, windowKey);

            // Сохраняем настройки при закрытии окна
            window.Closing += (sender, e) =>
            {
                SaveWindowSettings(window, windowKey);
            };

            // Сохраняем настройки при перемещении окна
            window.LocationChanged += (sender, e) =>
            {
                SaveWindowSettings(window, windowKey);
            };

            // Сохраняем настройки при изменении размера окна
            window.SizeChanged += (sender, e) =>
            {
                SaveWindowSettings(window, windowKey);
            };
        }

        /// <summary>
        /// Обновляет только состояние закрепленности окна
        /// </summary>
        /// <param name="window">Окно</param>
        /// <param name="windowKey">Уникальный ключ окна</param>
        /// <param name="isTopmost">Новое состояние закрепленности</param>
        public static void UpdateTopmostSetting(Window window, string windowKey, bool isTopmost)
        {
            if (_settings.TryGetValue(windowKey, out var settings))
            {
                settings.IsTopmost = isTopmost;
            }
            else
            {
                _settings[windowKey] = new WindowSettings
                {
                    Left = window.Left,
                    Top = window.Top,
                    Width = window.ActualWidth,
                    Height = window.ActualHeight,
                    IsTopmost = isTopmost
                };
            }
            SaveSettings();
        }

        /// <summary>
        /// Сохраняет числовое значение настройки для окна
        /// </summary>
        public static void SetDouble(string windowKey, string key, double value)
        {
            if (!_settings.TryGetValue(windowKey, out var settings))
            {
                settings = new WindowSettings();
                _settings[windowKey] = settings;
            }
            settings.Doubles[key] = value;
            SaveSettings();
        }

        /// <summary>
        /// Пытается получить числовое значение настройки для окна
        /// </summary>
        public static bool TryGetDouble(string windowKey, string key, out double value)
        {
            value = 0;
            if (_settings.TryGetValue(windowKey, out var settings) &&
                settings.Doubles != null &&
                settings.Doubles.TryGetValue(key, out var v))
            {
                value = v;
                return true;
            }
            return false;
        }
    }
}
