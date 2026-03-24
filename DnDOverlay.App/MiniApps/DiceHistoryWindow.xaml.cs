using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DnDOverlay.Infrastructure;

namespace DnDOverlay
{
    public partial class DiceHistoryWindow : Window
    {
        private const string WindowKey = "DiceHistoryWindow";
        private ObservableCollection<string>? _history;

        public DiceHistoryWindow()
        {
            InitializeComponent();

            UiScaleManager.RegisterWindow(this);

            WindowSettingsManager.RegisterWindow(this, WindowKey);
            TopmostToggleBinder.Bind(this, HeaderTopmostToggle);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            WindowResizeHelper.EnableResize(this);
        }

        public void SetHistorySource(ObservableCollection<string> history)
        {
            _history = history;
            HistoryList.ItemsSource = _history;
        }

        public void RefreshView()
        {
            if (_history == null)
            {
                return;
            }

            HistoryList.Items.Refresh();

            if (HistoryList.Items.Count > 0)
            {
                HistoryList.ScrollIntoView(HistoryList.Items[0]);
            }
        }

        private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseWindow()
        {
            Close();
        }

        public void SetTopmost(bool isTopmost)
        {
            Topmost = isTopmost;
            WindowSettingsManager.UpdateTopmostSetting(this, WindowKey, isTopmost);
        }

        public void ShowWindow()
        {
            Show();
        }

        public void CloseWindowExternal()
        {
            Close();
        }

        private void HeaderTopmostToggle_OnClick(object sender, RoutedEventArgs e)
        {
            var isTopmost = HeaderTopmostToggle.IsChecked == true;
            SetTopmost(isTopmost);
        }
    }
}
