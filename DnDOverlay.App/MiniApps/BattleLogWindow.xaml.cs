using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DnDOverlay.Infrastructure;
using BattleLogEntry = DnDOverlay.Infrastructure.InitiativeDataManager.BattleLogEntry;

namespace DnDOverlay
{
    public partial class BattleLogWindow : Window
    {
        private const string WindowKey = "BattleLogWindow";
        private ObservableCollection<BattleLogEntry>? _logSource;

        public event EventHandler? ClearLogRequested;

        public BattleLogWindow()
        {
            InitializeComponent();

            UiScaleManager.RegisterWindow(this);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            WindowResizeHelper.EnableResize(this);

            // Сохраняем/загружаем настройки окна
            WindowSettingsManager.RegisterWindow(this, WindowKey);
            MiniAppTopmostToggle.IsChecked = Topmost;
        }

        public void SetTopmost(bool isTopmost)
        {
            Topmost = isTopmost;
            MiniAppTopmostToggle.IsChecked = isTopmost;
            WindowSettingsManager.UpdateTopmostSetting(this, WindowKey, isTopmost);
        }

        public void SetLogSource(ObservableCollection<BattleLogEntry> source)
        {
            if (!ReferenceEquals(_logSource, source))
            {
                if (_logSource != null)
                {
                    _logSource.CollectionChanged -= LogSourceOnCollectionChanged;
                }

                _logSource = source;
                if (_logSource != null)
                {
                    _logSource.CollectionChanged += LogSourceOnCollectionChanged;
                }
            }

            BattleLogList.ItemsSource = _logSource;
            ScrollToEnd();
        }

        private void LogSourceOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                ScrollToEnd();
            }
        }

        public void ScrollToEnd()
        {
            if (_logSource == null || _logSource.Count == 0)
                return;

            var last = _logSource.Last();
            BattleLogList.ScrollIntoView(last);
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
            SetTopmost(isTopmost);
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ClearLog_OnClick(object sender, RoutedEventArgs e)
        {
            ClearLogRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
