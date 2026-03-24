using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DnDOverlay.Infrastructure;
using DnDOverlay.Theming;

namespace DnDOverlay
{
    public partial class AppSettingsWindow : Window
    {
        private const string WindowKey = "AppSettingsWindow";
        private static readonly double DefaultScale = UiScaleManager.DefaultScale;
        private const double CompactScale = 0.95;
        private const double LargeScale = 1.05;
        private const double Tolerance = 0.0001;
        private bool _isInitializing;

        public AppSettingsWindow()
        {
            InitializeComponent();
            _isInitializing = true;

            ThemeComboBox.ItemsSource = ThemeManager.Themes;
            var currentTheme = ThemeManager.CurrentTheme;
            ThemeComboBox.SelectedItem = ThemeManager.Themes.FirstOrDefault(t => t.Key == currentTheme.Key)
                                         ?? ThemeManager.DefaultTheme;

            AccentComboBox.ItemsSource = AccentColorManager.Accents;
            var currentAccent = AccentColorManager.CurrentAccent;
            AccentComboBox.SelectedItem = AccentColorManager.Accents.FirstOrDefault(a => a.Key == currentAccent.Key)
                                          ?? AccentColorManager.DefaultAccent;

            var savedScale = UiScaleManager.CurrentScale;
            SetSelectedScaleRadio(savedScale);
            UpdateScaleValueLabel(savedScale);

            UiScaleManager.RegisterWindow(this);
            WindowSettingsManager.RegisterWindow(this, WindowKey);
            _isInitializing = false;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            WindowResizeHelper.EnableResize(this, disableSystemResize: true);
            TopmostToggleBinder.Bind(this, MiniAppTopmostToggle);
        }

        private void ScaleRadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radioButton)
            {
                return;
            }

            if (!double.TryParse(radioButton.Tag as string ?? radioButton.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
            {
                return;
            }

            UpdateScaleValueLabel(scale);

            if (_isInitializing)
            {
                return;
            }

            UiScaleManager.SetScale(scale);
        }

        private void UpdateScaleValueLabel(double value)
        {
            if (ScaleValueTextBlock == null)
            {
                return;
            }

            var percent = Math.Round(value * 100);
            ScaleValueTextBlock.Text = percent.ToString("F0", CultureInfo.InvariantCulture) + "%";
        }

        private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            if (ThemeComboBox.SelectedItem is ThemeDefinition theme)
            {
                ThemeManager.ApplyTheme(theme.Key);
                AccentColorManager.RefreshResources();
            }
        }

        private void AccentComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            if (AccentComboBox.SelectedItem is AccentColorDefinition accent)
            {
                AccentColorManager.ApplyAccent(accent.Key);
            }
        }

        private void SaveButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ResetButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (ThemeComboBox.Items.Count > 0)
            {
                ThemeComboBox.SelectedItem = ThemeManager.DefaultTheme;
            }
            AccentComboBox.SelectedItem = AccentColorManager.DefaultAccent;
            BackgroundComboBox.SelectedIndex = 0;
            LanguageComboBox.SelectedIndex = 0;
            SetSelectedScaleRadio(DefaultScale);
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MiniAppTopmostToggle_OnClick(object sender, RoutedEventArgs e)
        {
            var isTopmost = MiniAppTopmostToggle.IsChecked == true;
            Topmost = isTopmost;
            WindowSettingsManager.UpdateTopmostSetting(this, WindowKey, isTopmost);
        }

        private void SetSelectedScaleRadio(double scale)
        {
            if (ScaleCompactRadio != null && Math.Abs(scale - CompactScale) < Tolerance)
            {
                ScaleCompactRadio.IsChecked = true;
                return;
            }

            if (ScaleLargeRadio != null && Math.Abs(scale - LargeScale) < Tolerance)
            {
                ScaleLargeRadio.IsChecked = true;
                return;
            }

            if (ScaleDefaultRadio != null)
            {
                ScaleDefaultRadio.IsChecked = true;
            }
        }
    }
}
