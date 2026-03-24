using System;
using System.Windows.Media;

namespace DnDOverlay.Theming
{
    public sealed class AccentColorDefinition
    {
        public AccentColorDefinition(string key, string displayName, Color primaryColor)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Accent key must be provided.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }

            Key = key;
            DisplayName = displayName;
            PrimaryColor = primaryColor;
            PrimaryForegroundColor = GetContrastingColor(primaryColor);
            HoverColor = Blend(primaryColor, Colors.White, 0.15);
            PressedColor = Blend(primaryColor, Colors.Black, 0.2);
            BorderColor = Blend(primaryColor, Colors.Black, 0.35);
            SubtleBackgroundColor = CreateSubtleBackground(primaryColor);
        }

        public string Key { get; }
        public string DisplayName { get; }
        public Color PrimaryColor { get; }
        public Color HoverColor { get; }
        public Color PressedColor { get; }
        public Color BorderColor { get; }
        public Color PrimaryForegroundColor { get; }
        public Color SubtleBackgroundColor { get; }

        private static Color Blend(Color source, Color target, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            return Color.FromArgb(
                source.A,
                (byte)Math.Clamp(source.R + (target.R - source.R) * amount, 0, 255),
                (byte)Math.Clamp(source.G + (target.G - source.G) * amount, 0, 255),
                (byte)Math.Clamp(source.B + (target.B - source.B) * amount, 0, 255));
        }

        private static Color CreateSubtleBackground(Color color)
        {
            var subtleAlpha = (byte)Math.Clamp((int)(color.A * 0.25), 32, 200);
            return Color.FromArgb(subtleAlpha, color.R, color.G, color.B);
        }

        private static Color GetContrastingColor(Color color)
        {
            var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return luminance > 0.6 ? Colors.Black : Colors.White;
        }
    }
}
