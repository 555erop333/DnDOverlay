using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using DnDOverlay.Infrastructure;
using DnDOverlay.MiniApps;

namespace DnDOverlay
{
    public partial class SoundsMiniAppWindow : Window, IMiniAppWindow
    {
        private const string WindowKey = "SoundsMiniAppWindow";
        private static readonly string[] SupportedExtensions = { ".ogg", ".mp3", ".wav", ".wma" };

        private WaveOutEvent? _waveOut;
        private WaveStream? _waveStream;
        private VolumeSampleProvider? _volumeProvider;
        private readonly ObservableCollection<object> _items = new();

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
            nameof(Volume),
            typeof(double),
            typeof(SoundsMiniAppWindow),
            new PropertyMetadata(0.6, OnVolumeChanged));

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        public SoundsMiniAppWindow()
        {
            InitializeComponent();

            UiScaleManager.RegisterWindow(this);

            WindowSettingsManager.RegisterWindow(this, WindowKey);

            SoundsList.ItemsSource = _items;
            Closed += OnWindowClosed;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            WindowResizeHelper.EnableResize(this);
            TopmostToggleBinder.Bind(this, MiniAppTopmostToggle);

            Volume = 0.6;
            LoadSounds();
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

        public void CloseWindow()
        {
            Close();
        }

        private void Window_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var source = e.OriginalSource as DependencyObject;
            if (source == null)
            {
                return;
            }

            if (FindAncestor<ButtonBase>(source) != null || FindAncestor<Slider>(source) != null)
            {
                return;
            }

            if (IsPointerNearResizeBorder(e))
            {
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private bool IsPointerNearResizeBorder(MouseButtonEventArgs e)
        {
            const double margin = 8d;
            var position = e.GetPosition(this);

            if (position.X <= margin || ActualWidth - position.X <= margin)
            {
                return true;
            }

            if (position.Y <= margin || ActualHeight - position.Y <= margin)
            {
                return true;
            }

            return false;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void MiniAppTopmostToggle_OnClick(object sender, RoutedEventArgs e)
        {
            var isTopmost = MiniAppTopmostToggle.IsChecked == true;
            SetTopmost(isTopmost);
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            CloseWindow();
        }

        private void SoundButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            var sound = button.Tag as SoundItem ?? button.DataContext as SoundItem;
            if (sound is null)
            {
                return;
            }

            PlaySound(sound);
        }

        private void LoadSounds()
        {
            _items.Clear();

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var audioDirectory = Path.Combine(baseDirectory, "Audio");

            if (!Directory.Exists(audioDirectory))
            {
                return;
            }

            var files = Directory.EnumerateFiles(audioDirectory)
                                  .Where(f => SupportedExtensions.Contains(Path.GetExtension(f) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                                  .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true));

            foreach (var file in files)
            {
                var displayName = Path.GetFileNameWithoutExtension(file) ?? "Звук";
                _items.Add(new SoundItem(displayName, file));
            }
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            StopPlayback();
            Closed -= OnWindowClosed;
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SoundsMiniAppWindow window)
            {
                window.UpdateVolume((double)e.NewValue);
            }
        }

        private void PlaySound(SoundItem sound)
        {
            try
            {
                StopPlayback();

                _waveStream = CreateWaveStream(sound.FilePath);
                _volumeProvider = new VolumeSampleProvider(_waveStream.ToSampleProvider())
                {
                    Volume = (float)Math.Clamp(Volume, 0.0, 1.0)
                };

                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += WaveOutOnPlaybackStopped;
                _waveOut.Init(_volumeProvider);
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                StopPlayback();
                MessageBox.Show(
                    this,
                    $"Не удалось воспроизвести звук \"{sound.DisplayName}\": {ex.Message}",
                    "Ошибка воспроизведения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static WaveStream CreateWaveStream(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

            return extension switch
            {
                ".ogg" => new VorbisWaveReader(filePath),
                ".wav" => new WaveFileReader(filePath),
                ".mp3" => new AudioFileReader(filePath),
                ".wma" => new MediaFoundationReader(filePath),
                _ => throw new NotSupportedException($"Формат {extension} не поддерживается."),
            };
        }

        private void UpdateVolume(double value)
        {
            var volume = (float)Math.Clamp(value, 0.0, 1.0);
            if (_volumeProvider is not null)
            {
                _volumeProvider.Volume = volume;
            }
        }

        private void StopPlayback()
        {
            if (_waveOut is not null)
            {
                _waveOut.PlaybackStopped -= WaveOutOnPlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            _volumeProvider = null;

            if (_waveStream is not null)
            {
                _waveStream.Dispose();
                _waveStream = null;
            }
        }

        private void WaveOutOnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            StopPlayback();
        }

        public record SoundItem(string DisplayName, string FilePath);
    }
}
