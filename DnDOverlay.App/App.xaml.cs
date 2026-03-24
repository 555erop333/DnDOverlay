using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DnDOverlay.Infrastructure;
using DnDOverlay.Theming;

namespace DnDOverlay
{
    public partial class App : Application
    {
        private const string MutexName = "DnDOverlay.App.Singleton";
        private Mutex? _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            _instanceMutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                _instanceMutex = null;
                MessageBox.Show("Приложение уже запущено.", "DnD Overlay", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            AppPaths.EnsureBaseDirectories();
            ThemeManager.Initialize();
            AccentColorManager.Initialize();

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomainOnUnhandledException;
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            _instanceMutex = null;
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowUnhandledException(e.Exception);
            e.Handled = true;
        }

        private void CurrentDomainOnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowUnhandledException(ex);
            }
            else
            {
                ShowUnhandledException(new Exception(e.ExceptionObject?.ToString() ?? "Неизвестная ошибка"));
            }
        }

        private static void ShowUnhandledException(Exception ex)
        {
            if (Current == null)
            {
                MessageBox.Show(ex.ToString(), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Current.Dispatcher.CheckAccess())
            {
                MessageBox.Show(ex.ToString(), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Current.Dispatcher.Invoke(() =>
                    MessageBox.Show(ex.ToString(), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
    }
}
