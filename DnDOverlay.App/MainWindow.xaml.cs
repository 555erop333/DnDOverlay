using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using DnDOverlay.Infrastructure;
using DnDOverlay.MiniApps;

namespace DnDOverlay
{
    public partial class MainWindow : Window
    {
        private const string WindowKey = "MainWindow";
        private readonly List<IMiniAppWindow> _miniAppWindows = new();

        public MainWindow()
        {
            InitializeComponent();
            
            UiScaleManager.RegisterWindow(this);
            
            // Регистрируем окно для автоматического сохранения/загрузки настроек
            WindowSettingsManager.RegisterWindow(this, WindowKey);
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var miniApp in _miniAppWindows.ToList())
            {
                miniApp.CloseWindow();
            }

            base.OnClosed(e);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            
            // Включаем изменение размера окна и отключаем системное снаппинг/масштабирование
            WindowResizeHelper.EnableResize(this, disableSystemResize: true);
            
            // Привязываем переключатель к текущему состоянию Topmost
            TopmostToggleBinder.Bind(this, TopmostToggle);
            
            MouseDown += (_, args) =>
            {
                if (args.ChangedButton == MouseButton.Left)
                {
                    DragMove();
                }
            };
        }

        private void TopmostToggle_OnClick(object sender, RoutedEventArgs e)
        {
            var isTopmost = TopmostToggle.IsChecked == true;
            Topmost = isTopmost;
            
            // Сохраняем настройку закрепленности
            WindowSettingsManager.UpdateTopmostSetting(this, WindowKey, isTopmost);
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenInitiative_OnClick(object sender, RoutedEventArgs e)
        {
            OpenMiniApp(() => new InitiativeMiniAppWindow());
        }

        private void OpenStats_OnClick(object sender, RoutedEventArgs e)
        {
            OpenMiniApp(() => new StatsMiniAppWindow());
        }

        private void OpenNotebook_OnClick(object sender, RoutedEventArgs e)
        {
            OpenMiniApp(() => new NotesMiniAppWindow());
        }

        private void OpenDice_OnClick(object sender, RoutedEventArgs e)
        {
            OpenMiniApp(() => new DiceMiniAppWindow());
        }

        private void OpenSounds_OnClick(object sender, RoutedEventArgs e)
        {
            OpenMiniApp(() => new SoundsMiniAppWindow());
        }

        private void OpenMusic_OnClick(object sender, RoutedEventArgs e)
        {
            OpenMiniApp(() => new MusicMiniAppWindow());
        }

        private void OpenSettings_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new AppSettingsWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void OpenMiniApp<TWindow>(Func<TWindow> factory)
            where TWindow : Window, IMiniAppWindow
        {
            var existing = _miniAppWindows.OfType<TWindow>().FirstOrDefault();
            if (existing is Window existingWindow)
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }

                existingWindow.Activate();
                existingWindow.Focus();
                return;
            }

            try
            {
                var window = factory();

                if (window is Window w)
                {
                    w.Closed += (_, _) => _miniAppWindows.Remove(window);
                }

                window.ShowWindow();
                _miniAppWindows.Add(window);
            }
            catch (XamlParseException xamlException)
            {
                MessageBox.Show(xamlException.ToString(), "Ошибка загрузки окна", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
