using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace DnDOverlay.Infrastructure
{
    public static class UiScaleManager
    {
        public const double MinScale = 0.95;
        public const double MaxScale = 1.05;
        public const double DefaultScale = 1.0;

        private const string SettingsFileName = "ui_settings.json";
        private static readonly string SettingsFilePath = AppPaths.GetDataFilePath(SettingsFileName);

        private static readonly List<WeakReference<FrameworkElement>> Targets = new();
        private static readonly List<WindowRegistration> WindowRegistrations = new();
        private static double _currentScale = LoadScale();

        public static double CurrentScale => _currentScale;

        public static event EventHandler<double>? ScaleChanged;

        public static void SetScale(double scale)
        {
            var clamped = Math.Clamp(scale, MinScale, MaxScale);
            if (Math.Abs(clamped - _currentScale) < 0.0001)
            {
                return;
            }

            _currentScale = clamped;
            SaveScale(clamped);
            ApplyScaleToTargets();
            ApplyScaleToWindows();
            ScaleChanged?.Invoke(null, clamped);
        }

        public static void RegisterWindow(Window window)
        {
            if (window == null)
            {
                return;
            }

            ApplyAndTrack(window.Content as FrameworkElement);

            var registration = GetOrCreateRegistration(window);
            CaptureBaseMetrics(window, registration);
            ApplyMinSizes(window, registration, _currentScale);
            ApplyInitialWindowSize(window, registration);

            window.ContentRendered += (_, _) =>
            {
                ApplyAndTrack(window.Content as FrameworkElement);
            };

            window.Closed += (_, _) => CleanupWindowRegistrations();
        }

        private static void ApplyAndTrack(FrameworkElement? element)
        {
            if (element == null)
            {
                return;
            }

            ApplyScale(element);

            if (Targets.Any(reference => reference.TryGetTarget(out var target) && ReferenceEquals(target, element)))
            {
                return;
            }

            Targets.Add(new WeakReference<FrameworkElement>(element));
            CleanupTargets();
        }

        private static void ApplyScaleToTargets()
        {
            CleanupTargets();
            foreach (var reference in Targets)
            {
                if (reference.TryGetTarget(out var element))
                {
                    ApplyScale(element);
                }
            }
        }

        private static void ApplyScaleToWindows()
        {
            CleanupWindowRegistrations();
            foreach (var registration in WindowRegistrations)
            {
                if (registration.WindowReference.TryGetTarget(out var window))
                {
                    ApplyWindowScale(window, registration, _currentScale);
                }
            }
        }

        private static void CleanupTargets()
        {
            Targets.RemoveAll(reference => !reference.TryGetTarget(out _));
        }

        private static void CleanupWindowRegistrations()
        {
            WindowRegistrations.RemoveAll(reg => !reg.WindowReference.TryGetTarget(out _));
        }

        private static void ApplyScale(FrameworkElement element)
        {
            if (element.LayoutTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.ScaleX = _currentScale;
                scaleTransform.ScaleY = _currentScale;
            }
            else
            {
                element.LayoutTransform = new ScaleTransform(_currentScale, _currentScale);
            }
        }

        private static double LoadScale()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<UiScaleSettings>(json);
                    var value = settings?.Scale ?? DefaultScale;
                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        return Math.Clamp(value, MinScale, MaxScale);
                    }
                }
            }
            catch
            {
                // ignore and fallback to default
            }

            return DefaultScale;
        }

        private static void SaveScale(double scale)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
                var settings = new UiScaleSettings { Scale = scale };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // ignore persistence failures
            }
        }

        private class UiScaleSettings
        {
            public double Scale { get; set; } = DefaultScale;
        }

        private class WindowRegistration
        {
            public WindowRegistration(Window window)
            {
                WindowReference = new WeakReference<Window>(window);
            }

            public WeakReference<Window> WindowReference { get; }
            public double BaseMinWidth { get; set; }
            public double BaseMinHeight { get; set; }
            public bool InitialSizeApplied { get; set; }
            public double LastScale { get; set; } = 1.0;
            public double BaseWidth { get; set; }
            public double BaseHeight { get; set; }
            public bool BaseMetricsCaptured { get; set; }
            public bool IsApplyingScale { get; set; }
        }

        private static WindowRegistration GetOrCreateRegistration(Window window)
        {
            CleanupWindowRegistrations();
            foreach (var registration in WindowRegistrations)
            {
                if (registration.WindowReference.TryGetTarget(out var existing) && ReferenceEquals(existing, window))
                {
                    return registration;
                }
            }

            var created = new WindowRegistration(window)
            {
                LastScale = _currentScale
            };
            WindowRegistrations.Add(created);
            return created;
        }

        private static void CaptureBaseMetrics(Window window, WindowRegistration registration)
        {
            if (!registration.BaseMetricsCaptured)
            {
                var baseWidth = ResolveBaseDimension(window.Width, window.ActualWidth);
                if (IsValidDimension(baseWidth))
                {
                    registration.BaseWidth = baseWidth;
                }

                var baseHeight = ResolveBaseDimension(window.Height, window.ActualHeight);
                if (IsValidDimension(baseHeight))
                {
                    registration.BaseHeight = baseHeight;
                }

                if (registration.BaseWidth > 0 || registration.BaseHeight > 0)
                {
                    registration.BaseMetricsCaptured = true;
                }
            }

            if (registration.BaseMinWidth <= 0 && window.MinWidth > 0)
            {
                registration.BaseMinWidth = window.MinWidth;
            }

            if (registration.BaseMinHeight <= 0 && window.MinHeight > 0)
            {
                registration.BaseMinHeight = window.MinHeight;
            }
        }

        private static void ApplyMinSizes(Window window, WindowRegistration registration, double scale)
        {
            if (registration.BaseMinWidth > 0)
            {
                window.MinWidth = registration.BaseMinWidth * scale;
            }

            if (registration.BaseMinHeight > 0)
            {
                window.MinHeight = registration.BaseMinHeight * scale;
            }
        }

        private static void ApplyInitialWindowSize(Window window, WindowRegistration registration)
        {
            if (registration.InitialSizeApplied)
            {
                return;
            }

            ApplyWindowSize(window, registration, _currentScale);
            registration.LastScale = _currentScale;
            registration.InitialSizeApplied = true;
        }

        private static void ApplyWindowScale(Window window, WindowRegistration registration, double newScale)
        {
            ApplyMinSizes(window, registration, newScale);
            ApplyWindowSize(window, registration, newScale);
            registration.LastScale = newScale;
        }

        private static void ApplyWindowSize(Window window, WindowRegistration registration, double scale)
        {
            if (scale <= 0)
            {
                scale = 1.0;
            }

            if (!registration.BaseMetricsCaptured)
            {
                CaptureBaseMetrics(window, registration);
            }

            registration.IsApplyingScale = true;
            try
            {
                if (IsValidDimension(registration.BaseWidth))
                {
                    var targetWidth = registration.BaseWidth * scale;
                    if (IsValidDimension(targetWidth))
                    {
                        if (IsValidDimension(window.MinWidth))
                        {
                            targetWidth = Math.Max(window.MinWidth, targetWidth);
                        }
                        window.Width = targetWidth;
                    }
                }

                if (IsValidDimension(registration.BaseHeight))
                {
                    var targetHeight = registration.BaseHeight * scale;
                    if (IsValidDimension(targetHeight))
                    {
                        if (IsValidDimension(window.MinHeight))
                        {
                            targetHeight = Math.Max(window.MinHeight, targetHeight);
                        }
                        window.Height = targetHeight;
                    }
                }
            }
            finally
            {
                registration.IsApplyingScale = false;
            }
        }

        private static double ResolveBaseDimension(double explicitValue, double actualValue)
        {
            if (IsValidDimension(explicitValue))
            {
                return explicitValue;
            }

            if (IsValidDimension(actualValue))
            {
                return actualValue;
            }

            return 0;
        }

        private static bool IsValidDimension(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
        }
    }
}
