using System.Windows.Media;

namespace DnDOverlay.Theming
{
    public class ThemeDefinition
    {
        public ThemeDefinition(
            string key,
            string displayName,
            Color appWindowBackground,
            Color appWindowBorder,
            Color headerBackground,
            Color headerText,
            Color controlButtonBackground,
            Color controlButtonBorder,
            Color controlButtonForeground,
            Color controlButtonHoverBackground,
            Color controlButtonPressedBackground,
            Color surfaceBackground,
            Color surfaceForeground,
            Color panelBackground,
            Color panelBorder,
            Color inputBackground,
            Color inputForeground,
            Color statusGoodBackground,
            Color statusGoodForeground,
            Color statusWarningBackground,
            Color statusWarningForeground,
            Color statusDangerBackground,
            Color statusDangerForeground)
        {
            Key = key;
            DisplayName = displayName;
            AppWindowBackground = appWindowBackground;
            AppWindowBorder = appWindowBorder;
            HeaderBackground = headerBackground;
            HeaderText = headerText;
            ControlButtonBackground = controlButtonBackground;
            ControlButtonBorder = controlButtonBorder;
            ControlButtonForeground = controlButtonForeground;
            ControlButtonHoverBackground = controlButtonHoverBackground;
            ControlButtonPressedBackground = controlButtonPressedBackground;
            SurfaceBackground = surfaceBackground;
            SurfaceForeground = surfaceForeground;
            PanelBackground = panelBackground;
            PanelBorder = panelBorder;
            InputBackground = inputBackground;
            InputForeground = inputForeground;
            StatusGoodBackground = statusGoodBackground;
            StatusGoodForeground = statusGoodForeground;
            StatusWarningBackground = statusWarningBackground;
            StatusWarningForeground = statusWarningForeground;
            StatusDangerBackground = statusDangerBackground;
            StatusDangerForeground = statusDangerForeground;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public Color AppWindowBackground { get; }
        public Color AppWindowBorder { get; }
        public Color HeaderBackground { get; }
        public Color HeaderText { get; }
        public Color ControlButtonBackground { get; }
        public Color ControlButtonBorder { get; }
        public Color ControlButtonForeground { get; }
        public Color ControlButtonHoverBackground { get; }
        public Color ControlButtonPressedBackground { get; }
        public Color SurfaceBackground { get; }
        public Color SurfaceForeground { get; }
        public Color PanelBackground { get; }
        public Color PanelBorder { get; }
        public Color InputBackground { get; }
        public Color InputForeground { get; }
        public Color StatusGoodBackground { get; }
        public Color StatusGoodForeground { get; }
        public Color StatusWarningBackground { get; }
        public Color StatusWarningForeground { get; }
        public Color StatusDangerBackground { get; }
        public Color StatusDangerForeground { get; }

        public override string ToString() => DisplayName;
    }
}
