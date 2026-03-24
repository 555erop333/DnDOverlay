using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using DnDOverlay.Infrastructure;
using DnDOverlay.MiniApps;
using LibVLCSharp.Shared;
using TagLib;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using WpfTextBox = System.Windows.Controls.TextBox;
using IOFile = System.IO.File;

namespace DnDOverlay
{
    public partial class MusicMiniAppWindow : Window, IMiniAppWindow
    {
        private const string WindowKey = "MusicMiniAppWindow";
        private static readonly string[] SupportedExtensions = { ".ogg", ".mp3", ".wav", ".wma", ".flac" };
        private static readonly string[] PreferredStreamExtensions = { "m4a", "mp4", "aac", "mp3", "m3u8" };
        private static readonly string[] PreferredStreamCodecs = { "mp4a", "aac" };
        private static readonly string[] UnsupportedStreamExtensions = { "webm", "mkv", "mka" };
        private static readonly string[] UnsupportedStreamCodecs = { "opus", "vorbis" };
        private static readonly string[] CompatibleStreamExtensions = { "m4a", "mp4", "aac", "mp3", "wma", "wav", "flac", "m3u8" };
        private static readonly string[] CompatibleStreamCodecs = { "mp4a", "aac", "mp3", "flac", "alac", "wma" };
        private static readonly string AppBaseDirectory = AppPaths.BaseDirectory;
        private static readonly string DefaultMusicDirectory = AppPaths.MusicDirectory;
        private static readonly string DefaultAudioDirectory = AppPaths.AudioDirectory;
        private static readonly string BackgroundMusicDirectory = AppPaths.BackgroundDirectory;
        private static readonly string YouTubeCacheDirectory = Path.Combine(AppPaths.MusicDirectory, "YouTube");
        private static readonly string YouTubeWebViewUserDataFolder = AppPaths.WebViewUserDataDirectory;
        private static readonly string YouTubeCookiesFilePath = AppPaths.GetDataFilePath("youtube_cookies.txt");
        private static readonly Lazy<string?> DefaultCookiesBrowser = new(GetDefaultCookiesBrowser, LazyThreadSafetyMode.ExecutionAndPublication);
        private const string YtDlpFallbackUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private const string YouTubePreviewSuffix = "_preview";
        private const string YouTubeBufferSuffix = "_buffer";
        private const string YouTubeSegmentSuffix = "_seg";
        private static readonly Uri YouTubeHomeUri = new("https://m.youtube.com", UriKind.Absolute);
        private const string WebView2RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
        private const string YouTubeAdBlockerScript = @"(function () {
    if (window.__dndOverlayYouTubeAdblock) {
        return;
    }
    window.__dndOverlayYouTubeAdblock = true;

    const hideSelectors = [
        '.ytp-ad-module',
        '.ytp-ad-player-overlay',
        '.ytp-ad-overlay-container',
        '#player-ads',
        'ytd-promoted-video-renderer',
        'ytd-search-pyv-renderer',
        'ytd-display-ad-renderer',
        '.ytd-promoted-sparkles-web-renderer',
        '.ytp-ad-button',
        '.ytp-ad-timed-pie-countdown',
        '.ytp-ad-text',
        '.masthead-ad',
        '.ytp-ad-skip-button-container',
        '.html5-endscreen',
        '.ytp-endscreen-content'
    ];

    const css = document.createElement('style');
    css.textContent = hideSelectors.map(selector => `${selector} { display: none !important; visibility: hidden !important; opacity: 0 !important; }`).join('\n') + '\n.ad-showing .html5-video-container video { filter: none !important; }';
    document.documentElement.appendChild(css);

    const clickSkip = () => {
        document.querySelectorAll('.ytp-ad-skip-button, .ytp-ad-skip-button-modern, .ytp-ad-overlay-close-button, .ytp-ad-skip-button-container').forEach(btn => {
            try {
                btn.click();
            } catch (e) {}
        });
    };

    const fastForwardAd = () => {
        const video = document.querySelector('video');
        if (!video) {
            return;
        }
        const isAd = document.body && document.body.classList && document.body.classList.contains('ad-showing');
        if (isAd) {
            try {
                video.muted = true;
                if (video.duration && isFinite(video.duration)) {
                    video.currentTime = video.duration;
                }
                video.playbackRate = 16;
            } catch (e) {}
        } else if (video.playbackRate > 1) {
            video.playbackRate = 1;
            video.muted = false;
        }
    };

    const cleanSponsored = () => {
        document.querySelectorAll('#contents ytd-shelf-renderer:has(ytd-promoted-video-renderer)').forEach(el => el.remove());
        document.querySelectorAll('ytd-promoted-sparkles-text-search-renderer, ytd-promoted-video-renderer, ytd-search-pyv-renderer').forEach(el => el.remove());
    };

    const removeAds = () => {
        hideSelectors.forEach(selector => {
            document.querySelectorAll(selector).forEach(el => {
                if (el && el.remove) {
                    el.remove();
                }
            });
        });
        clickSkip();
        cleanSponsored();
        fastForwardAd();
    };

    const observer = new MutationObserver(() => removeAds());
    observer.observe(document.documentElement || document.body, { childList: true, subtree: true });

    window.addEventListener('yt-navigate-finish', removeAds, true);
    window.addEventListener('load', removeAds, true);
    setInterval(removeAds, 750);

    removeAds();
})();";
        private static readonly string[] YouTubeAdBlockPatterns =
        {
            "https://*.doubleclick.net/*",
            "https://*.googlesyndication.com/*",
            "https://*.googleads.g.doubleclick.net/*",
            "https://*.youtube.com/api/stats/ads/*",
            "https://*.youtube.com/pagead/*",
            "https://*.youtube.com/get_midroll_info*",
            "https://*.youtube.com/ptracking*",
            "https://*.youtube.com/api/stats/qoe?adcontext=1*"
        };
        private static readonly string[] YouTubeAdUrlFragments =
        {
            "doubleclick.net",
            "googlesyndication.com",
            "googleads.g.doubleclick.net",
            "youtube.com/api/stats/ads",
            "youtube.com/pagead/",
            "youtube.com/get_midroll_info",
            "adcontext=1",
            "ptracking"
        };
        private const int PreviewDurationSeconds = 60;
        private const int BufferedAdditionalSeconds = 300;
        private const int BufferedTotalSeconds = PreviewDurationSeconds + BufferedAdditionalSeconds;
        private const int SegmentDurationSeconds = BufferedAdditionalSeconds;
        private const int MaxSegmentFilesPerTrack = 2;
        private static readonly ConcurrentDictionary<string, byte> PreviewDownloadsInFlight = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> BufferDownloadsInFlight = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> SegmentDownloadsInFlight = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, bool> SegmentDownloadDisabledVideos = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan SegmentUpdateThrottle = TimeSpan.FromSeconds(2);
        private static readonly SemaphoreSlim SegmentDownloadSemaphore = new(1, 1);
        private const int SegmentDownloadTimeoutMilliseconds = 60000;

        private readonly ObservableCollection<PlaylistItem> _playlist = new();
        private readonly ObservableCollection<YouTubeTrack> _youTubeTracks = new();
        private MusicDataManager.MusicMetadata _meta = new();
        private string _musicDirectory = string.Empty;
        private enum PlaylistLibraryKind
        {
            Downloaded,
            Background
        }

        private PlaylistLibraryKind _currentPlaylistLibrary = PlaylistLibraryKind.Downloaded;
        private TabItem? _playlistTabHost;
        private PlaylistItem? _selectedItem;
        private YouTubeTrack? _selectedYouTubeTrack;
        private bool _downloadAllYouTubeInProgress;
        private static readonly Regex TimecodeRegex = new(@"\b(?:(?<hours>\d{1,2}):)?(?<minutes>\d{1,2}):(?<seconds>\d{1,2})\b", RegexOptions.Compiled);
        private static readonly Regex YtDlpProgressRegex = new(@"\b(?<percent>\d{1,3}(?:\.\d+)?)%", RegexOptions.Compiled);
        private static readonly SolidColorBrush TimecodeLinkBrush = CreateTimecodeBrush();
        private Hyperlink? _activeTimecodeHyperlink;
        private Point _hyperlinkDragStart;
        private bool _hyperlinkDragInProgress;
        private bool _cookiesButtonRequested;

        private readonly DeckState _deck1 = new();
        private readonly DeckState _deck2 = new();
        private readonly DeckState _deck3 = new();
        private readonly DeckState _deck4 = new();
        private static readonly Lazy<LibVlcContext> LibVlc = new(static () => LibVlcContext.Create(), LazyThreadSafetyMode.ExecutionAndPublication);

        private static LibVlcContext Vlc => LibVlc.Value;

        private readonly DispatcherTimer _positionTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
        private bool _isUpdatingSliders;
        private bool _deck1UserSeeking;
        private bool _deck2UserSeeking;
        private bool _deck3UserSeeking;
        private bool _deck4UserSeeking;
        private bool _isPopulatingDevices;
        private TextBlock? _activeDeckLabelDrag;
        private Point _youTubeDragStart;
        private List<DeviceItem> _outputDevices = new();
        private readonly DispatcherTimer _playlistRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        private readonly DispatcherTimer _volumeSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
        private readonly Dictionary<string, double> _pendingVolumeSaves = new();
        private bool _playlistRefreshScheduled;
        private readonly ConcurrentDictionary<string, DateTime> _segmentUpdateDebounce = new(StringComparer.OrdinalIgnoreCase);
        private FileSystemWatcher? _musicWatcher;
        private string _watchedDirectory = string.Empty;
        private GridLength _leftSavedWidth;
        private GridLength _rightSavedWidth;
        private GridLength _leftDefaultWidth;
        private GridLength _rightDefaultWidth;
        private GridLength _splitterDefaultWidth;
        private GridLength _splitterSavedWidth;
        private double _leftDefaultMinWidth;
        private double _rightDefaultMinWidth;
        private bool _leftCollapsed;
        private bool _rightCollapsed;
        private double _lastLeftRatio;
        private bool _suppressToggleHandlers = false;
        private string? _lastHoverElementDescription;
        private static readonly string DeckStateFilePath = AppPaths.GetDataFilePath("music_deck_state.json");
        private static readonly JsonSerializerOptions DeckStateSerializerOptions = new() { WriteIndented = true };
        private static readonly string SessionLogFilePath;
        private static readonly string YouTubeTracksFilePath = AppPaths.GetDataFilePath("youtube_tracks.json");
        private static readonly JsonSerializerOptions YouTubeTracksSerializerOptions = new() { PropertyNameCaseInsensitive = true };
        private bool _externalToolsReady;
        private CancellationTokenSource? _toolInstallCts;
        private Task<bool>? _toolInstallTask;
        private bool _youTubeWebViewInitializationFailed;
        private bool _youTubeAdBlockConfigured;
        private bool _youTubeAdScriptInjected;

        static MusicMiniAppWindow()
        {
            var logDir = AppPaths.LogsDirectory;
            var sessionName = $"music_mini_app_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
            SessionLogFilePath = Path.Combine(logDir, sessionName);
            try
            {
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                IOFile.AppendAllText(SessionLogFilePath, $"[MusicMiniApp] {DateTime.Now:HH:mm:ss.fff} Session started\n");
            }
            catch
            {
                // ignore
            }
        }

        private static string BuildElementDescription(DependencyObject source)
        {
            var parts = new List<string>();
            var current = source;
            while (current != null && parts.Count < 8)
            {
                string descriptor;
                if (current is FrameworkElement fe)
                {
                    descriptor = string.IsNullOrWhiteSpace(fe.Name)
                        ? fe.GetType().Name
                        : $"{fe.GetType().Name}#{fe.Name}";
                }
                else if (current is FrameworkContentElement fce)
                {
                    descriptor = string.IsNullOrWhiteSpace(fce.Name)
                        ? fce.GetType().Name
                        : $"{fce.GetType().Name}#{fce.Name}";
                }
                else
                {
                    descriptor = current.GetType().Name;
                }

                parts.Add(descriptor);

                current = VisualTreeHelper.GetParent(current) ?? (current as FrameworkContentElement)?.Parent;
            }

            return parts.Count == 0 ? source.GetType().Name : string.Join(" > ", parts);
        }

        private static void LogDebug(string message)
        {
            var line = $"[MusicMiniApp] {DateTime.Now:HH:mm:ss.fff} {message}";
            Debug.WriteLine(line);
            try
            {
                var logDir = Path.GetDirectoryName(SessionLogFilePath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                IOFile.AppendAllText(SessionLogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // ignore logging failures
            }
        }

        private static bool DownloadSegmentSnippet(string link, string videoId, int segmentIndex, string destination)
        {
            try
            {
                var command = BuildYtDlpSegmentCommand(link, videoId, segmentIndex, destination);
                var commandSucceeded = TryRunYtDlp(command, out _, out _, timeoutMilliseconds: SegmentDownloadTimeoutMilliseconds);

                if (!IOFile.Exists(destination))
                {
                    return false;
                }

                if (!commandSucceeded)
                {
                    LogDebug($"DownloadSegmentSnippet detected output despite yt-dlp error for {videoId} seg#{segmentIndex}, treating as success.");
                }

                TrimPreviewSnippet(destination, SegmentDurationSeconds);
                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"DownloadSegmentSnippet failed for {videoId} seg#{segmentIndex}: {ex.Message}");
            }

            return false;
        }

        private async void YouTubeDownloadPreviewButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not YouTubeTrack track)
            {
                return;
            }

            if (!await EnsureExternalToolsAvailableAsync())
            {
                MessageBox.Show(this,
                    "Не удалось подготовить инструменты для скачивания превью.",
                    "YouTube превью",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            button.IsEnabled = false;
            try
            {
                var previewPath = await Task.Run(() => EnsureYouTubePreview(track));
                if (string.IsNullOrWhiteSpace(previewPath))
                {
                    MessageBox.Show(this,
                        "Не удалось скачать минутное превью. Попробуйте ещё раз.",
                        "YouTube превью",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"YouTubeDownloadPreviewButton_OnClick failed: {ex.Message}");
                MessageBox.Show(this,
                    "Произошла ошибка при скачивании превью.",
                    "YouTube превью",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private async Task UpgradePreviewToBufferedPlaybackAsync(DeckState deck, YouTubeTrack track)
        {
            try
            {
                async Task<PlaylistItem?> BuildUpgradedPlaylistItemAsync(string context)
                {
                    try
                    {
                        return await Task.Run(() => CreatePlaylistItemFromYouTube(track, allowPreviewFallback: false)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"UpgradePreviewToBufferedPlaybackAsync failed to create playlist item ({context}) for {track.DisplayName}: {ex.Message}");
                        return null;
                    }
                }

                var streamingUpgraded = await Task.Run(() =>
                {
                    if (TryResolveStreamingUrl(track, out var streamLink, out var headers))
                    {
                        track.UpdateStreamUrl(streamLink);
                        track.UpdateStreamHeaders(headers);
                        UpdateYouTubeTrackStorageStreamUrl(track.VideoId, streamLink);
                        return true;
                    }

                    return false;
                }).ConfigureAwait(false);

                if (streamingUpgraded)
                {
                    var finalItem = await BuildUpgradedPlaylistItemAsync("streaming").ConfigureAwait(false);
                    if (finalItem == null)
                    {
                        return;
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        var resumeSeconds = GetDeckPlaybackPositionSeconds(deck);
                        var resumePlaying = deck.IsPlaying;
                        PlayPlaylistItemOnDeck(deck, finalItem, resumeSeconds, resumePlaying);
                        LogDebug($"Upgraded preview to streaming track '{track.DisplayName}' at {resumeSeconds:F1}s.");
                    });
                }
                else
                {
                    var bufferPath = await EnsureYouTubeBufferAsync(track).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(bufferPath))
                    {
                        return;
                    }

                    track.UpdateStreamUrl(bufferPath);
                    track.UpdateStreamHeaders(new List<KeyValuePair<string, string>>());
                    UpdateYouTubeTrackStorageStreamUrl(track.VideoId, bufferPath);
                    var finalItem = await BuildUpgradedPlaylistItemAsync("buffer").ConfigureAwait(false);
                    if (finalItem == null)
                    {
                        return;
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        var resumeSeconds = GetDeckPlaybackPositionSeconds(deck);
                        var resumePlaying = deck.IsPlaying;
                        PlayPlaylistItemOnDeck(deck, finalItem, resumeSeconds, resumePlaying);
                        LogDebug($"Upgraded preview to buffered track '{track.DisplayName}' at {resumeSeconds:F1}s.");
                    });
                }

                _ = Task.Run(() => EnsureYouTubeBuffer(track));
            }
            catch (Exception ex)
            {
                LogDebug($"UpgradePreviewToBufferedPlaybackAsync failed for {track.DisplayName}: {ex.Message}");
            }
        }

        private void YouTubeCookiesButton_OnClick(object sender, RoutedEventArgs e)
        {
            LogDebug("YouTubeCookiesButton_OnClick invoked");
            var dialog = new YouTubeCookieLoginWindow(YouTubeCookiesFilePath)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                LogDebug("YouTube cookie dialog returned success");
                MessageBox.Show(this,
                    "Куки YouTube сохранены",
                    "Авторизация YouTube",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _cookiesButtonRequested = false;
                UpdateYouTubeCookiesIndicator();
            }
            else
            {
                LogDebug("YouTube cookie dialog cancelled");
            }
        }

        private void UpdateYouTubeCookiesIndicator()
        {
            LogDebug("UpdateYouTubeCookiesIndicator invoked");
            bool hasCookies = false;
            try
            {
                if (TryReadYouTubeCookies(out var cookies))
                {
                    hasCookies = cookies.Count > 0;
                    LogDebug($"Cookies read: {(hasCookies ? cookies.Count.ToString() : "0")}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"UpdateYouTubeCookiesIndicator failed: {ex.Message}");
            }

            Dispatcher.Invoke(() =>
            {
                var buttonShouldBeVisible = !hasCookies || _cookiesButtonRequested;
                YouTubeCookiesButton.Visibility = buttonShouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                YouTubeCookiesSavedIcon.Visibility = hasCookies ? Visibility.Visible : Visibility.Collapsed;
                LogDebug($"Cookies indicator updated: hasCookies={hasCookies}, buttonVisible={buttonShouldBeVisible}");

                if (buttonShouldBeVisible && YouTubeCookiesButton.Visibility == Visibility.Visible && YouTubeCookiesButton.IsEnabled)
                {
                    YouTubeCookiesButton.Focus();
                    LogDebug("Cookies button focused for user action");
                }
            });
        }

        private void YouTubeCookiesSavedIcon_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            LogDebug("YouTubeCookiesSavedIcon clicked");
            _cookiesButtonRequested = true;
            UpdateYouTubeCookiesIndicator();
        }

        private void YouTubeCookiesButton_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (YouTubeCookiesSavedIcon.Visibility == Visibility.Visible && _cookiesButtonRequested)
            {
                LogDebug("YouTubeCookiesButton lost focus, hiding button again");
                _cookiesButtonRequested = false;
                UpdateYouTubeCookiesIndicator();
            }
        }

        private const string YtDlpMetadataBaseArguments = "--dump-single-json --no-playlist --skip-download --force-ipv4 --socket-timeout 20 --format bestaudio[ext=m4a]/bestaudio/best ";

        private static YtDlpCommand BuildYtDlpMetadataCommand(string link)
        {
            var cookiesArg = GetCookiesArgument(out var usesCookieFile);
            var baseBuilder = new StringBuilder(YtDlpMetadataBaseArguments);
            baseBuilder.Append(cookiesArg);
            AppendAuthorizationHeaders(baseBuilder, link);

            if (usesCookieFile)
            {
                AppendAndroidMusicClientArgs(baseBuilder, includeFormatAndHls: false);
                LogDebug("Using android_music client arguments for metadata request with cookie file.");
            }

            var variants = new List<CommandVariant>
            {
                new(baseBuilder.ToString().Trim(), null)
            };

            if (usesCookieFile)
            {
                var androidFallback = new StringBuilder(YtDlpMetadataBaseArguments);
                androidFallback.Append(cookiesArg);
                AppendAuthorizationHeaders(androidFallback, link);
                AppendAndroidClientArgs(androidFallback);
                variants.Add(new CommandVariant(androidFallback.ToString().Trim(), "Applying yt-dlp fallback (android client with authenticated cookies)."));

                var defaultFallback = new StringBuilder(YtDlpMetadataBaseArguments);
                defaultFallback.Append(cookiesArg);
                AppendAuthorizationHeaders(defaultFallback, link);
                AppendDefaultClientArgs(defaultFallback);
                variants.Add(new CommandVariant(defaultFallback.ToString().Trim(), "Applying yt-dlp fallback (default client with authenticated cookies)."));
            }
            else
            {
                var fallbackArgs = Build403FallbackArguments(link, usesCookieFile);
                if (!string.IsNullOrWhiteSpace(fallbackArgs))
                {
                    var combined = ($"{baseBuilder.ToString().Trim()} {fallbackArgs}").Trim();
                    variants.Add(new CommandVariant(combined, "Applying yt-dlp fallback (android client)."));
                }
            }

            return new YtDlpCommand(link, variants, usesCookieFile);
        }

        private static YtDlpCommand BuildYtDlpDownloadCommand(string link, string template)
        {
            const string baseArgs = "--no-playlist --force-overwrites --extract-audio --audio-format m4a --audio-quality 0 --force-ipv4 --socket-timeout 20 --newline ";
            var cookiesArg = GetCookiesArgument(out var usesCookieFile);
            var baseBuilder = new StringBuilder(baseArgs);
            baseBuilder.Append(cookiesArg);
            AppendAuthorizationHeaders(baseBuilder, link);
            AppendAndroidMusicClientArgs(baseBuilder, includeFormatAndHls: true);
            baseBuilder.Append("--output \"").Append(template).Append("\"");
            LogDebug(usesCookieFile
                ? "Using android_music client arguments for download request with cookie file."
                : "Using default download arguments (no authentication cookies).");

            var variants = new List<CommandVariant>
            {
                new(baseBuilder.ToString().Trim(), null)
            };

            if (usesCookieFile)
            {
                var androidFallback = new StringBuilder(baseArgs);
                androidFallback.Append(cookiesArg);
                AppendAuthorizationHeaders(androidFallback, link);
                AppendAndroidClientArgs(androidFallback);
                AppendHlsFormatArgs(androidFallback);
                androidFallback.Append("--output \"").Append(template).Append("\"");
                variants.Add(new CommandVariant(androidFallback.ToString().Trim(), "Applying yt-dlp fallback (android client download with authenticated cookies)."));

                var defaultFallback = new StringBuilder(baseArgs);
                defaultFallback.Append(cookiesArg);
                AppendAuthorizationHeaders(defaultFallback, link);
                AppendDefaultClientArgs(defaultFallback);
                AppendHlsFormatArgs(defaultFallback);
                defaultFallback.Append("--output \"").Append(template).Append("\"");
                variants.Add(new CommandVariant(defaultFallback.ToString().Trim(), "Applying yt-dlp fallback (default client download with authenticated cookies)."));
            }
            else
            {
                var fallbackArgs = Build403FallbackArguments(link, usesCookieFile);
                if (!string.IsNullOrWhiteSpace(fallbackArgs))
                {
                    var combined = ($"{baseBuilder.ToString().Trim()} {fallbackArgs}").Trim();
                    variants.Add(new CommandVariant(combined, "Applying yt-dlp fallback (android client)."));
                }
            }

            return new YtDlpCommand(link, variants, usesCookieFile);
        }

        private static string? Build403FallbackArguments(string link, bool usesCookieFile)
        {
            if (usesCookieFile)
            {
                // Authentication cookies already supplied; metadata command handles fallback separately.
                return null;
            }

            var fallback = new StringBuilder();

            fallback.Append("--extractor-args \"youtube:player_client=android\" --user-agent \"");
            fallback.Append(YtDlpFallbackUserAgent);
            fallback.Append("\" --add-header \"Origin: https://www.youtube.com\"");

            if (!string.IsNullOrWhiteSpace(link))
            {
                fallback.Append(" --add-header \"Referer: ")
                        .Append(EscapeArgument(link))
                        .Append("\"");
            }

            return fallback.Length == 0 ? null : fallback.ToString().Trim();
        }

        private static string EscapeArgument(string value)
        {
            return value.Replace("\"", "\\\"");
        }

        private static string GetCookiesArgument(out bool usesCookieFile)
        {
            usesCookieFile = false;

            try
            {
                if (!string.IsNullOrWhiteSpace(YouTubeCookiesFilePath) && System.IO.File.Exists(YouTubeCookiesFilePath))
                {
                    usesCookieFile = true;
                    LogDebug($"Using local YouTube cookies file at {YouTubeCookiesFilePath}.");
                    return $"--cookies \"{EscapeArgument(YouTubeCookiesFilePath)}\" ";
                }
            }
            catch (Exception ex)
            {
                LogDebug($"GetCookiesArgument file check failed: {ex.Message}");
            }

            var browser = DefaultCookiesBrowser.Value;
            if (string.IsNullOrWhiteSpace(browser))
            {
                LogDebug("No cookies source available; proceeding without authentication cookies.");
                return string.Empty;
            }

            LogDebug($"Detected {browser} for cookies (no local file).");
            return $"--cookies-from-browser {browser} ";
        }

        private static void AppendAuthorizationHeaders(StringBuilder args, string link)
        {
            if (!TryBuildSapSidHashHeader(out var sapSidHash))
            {
                return;
            }

            const string origin = "https://www.youtube.com";
            args.Append($"--add-header \"Authorization: SAPISIDHASH {sapSidHash}\" ");
            args.Append($"--add-header \"Origin: {origin}\" ");
            args.Append($"--add-header \"X-Origin: {origin}\" ");
            args.Append("--add-header \"X-Goog-AuthUser: 0\" ");
            args.Append("--add-header \"X-YouTube-Client-Name: 67\" ");
            args.Append("--add-header \"X-YouTube-Client-Version: 1.20240801.01.00\" ");
            args.Append("--add-header \"User-Agent: ")
                .Append(EscapeArgument(YtDlpFallbackUserAgent))
                .Append("\" ");
            if (!string.IsNullOrWhiteSpace(link))
            {
                args.Append("--add-header \"Referer: ")
                    .Append(EscapeArgument(link))
                    .Append("\" ");
            }
        }

        private static void AppendAndroidMusicClientArgs(StringBuilder args, bool includeFormatAndHls)
        {
            args.Append("--extractor-args \"youtube:player_client=android_music\" ");
            if (includeFormatAndHls)
            {
                AppendHlsFormatArgs(args);
            }
        }

        private static void AppendAndroidClientArgs(StringBuilder args)
        {
            args.Append("--extractor-args \"youtube:player_client=android\" ");
        }

        private static void AppendDefaultClientArgs(StringBuilder args)
        {
            args.Append("--extractor-args \"youtube:player_client=default\" ");
        }

        private static void AppendHlsFormatArgs(StringBuilder args)
        {
            args.Append("--format \"bestaudio[protocol^=m3u8]/bestaudio/best\" ");
            args.Append("--hls-use-mpegts --concurrent-fragments 1 ");
        }

        private static List<KeyValuePair<string, string>> BuildYouTubeStreamHeaders(string? referer)
        {
            var headers = new List<KeyValuePair<string, string>>();

            if (TryBuildSapSidHashHeader(out var sapSidHash))
            {
                headers.Add(new KeyValuePair<string, string>("Authorization", $"SAPISIDHASH {sapSidHash}"));
            }

            const string origin = "https://www.youtube.com";
            headers.Add(new KeyValuePair<string, string>("Origin", origin));
            headers.Add(new KeyValuePair<string, string>("X-Origin", origin));
            headers.Add(new KeyValuePair<string, string>("X-Goog-AuthUser", "0"));
            headers.Add(new KeyValuePair<string, string>("X-YouTube-Client-Name", "67"));
            headers.Add(new KeyValuePair<string, string>("X-YouTube-Client-Version", "1.20240801.01.00"));
            headers.Add(new KeyValuePair<string, string>("User-Agent", YtDlpFallbackUserAgent));

            if (!string.IsNullOrWhiteSpace(referer))
            {
                headers.Add(new KeyValuePair<string, string>("Referer", referer));
            }

            if (TryBuildCookieHeader(out var cookieHeader))
            {
                headers.Add(new KeyValuePair<string, string>("Cookie", cookieHeader));
            }

            return headers;
        }

        private static bool TryReadYouTubeCookies(out Dictionary<string, string> cookies)
        {
            cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!System.IO.File.Exists(YouTubeCookiesFilePath))
                {
                    return false;
                }

                foreach (var rawLine in System.IO.File.ReadLines(YouTubeCookiesFilePath))
                {
                    if (string.IsNullOrWhiteSpace(rawLine))
                    {
                        continue;
                    }

                    var line = rawLine;
                    if (line.StartsWith("#HttpOnly_", StringComparison.Ordinal))
                    {
                        line = line.Substring("#HttpOnly_".Length);
                    }

                    if (line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parts = line.Split('\t');
                    if (parts.Length < 7)
                    {
                        continue;
                    }

                    var name = parts[5];
                    var value = parts[6];
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    cookies[name.Trim()] = value?.Trim() ?? string.Empty;
                }

                return cookies.Count > 0;
            }
            catch (Exception ex)
            {
                LogDebug($"TryReadYouTubeCookies failed: {ex.Message}");
                cookies.Clear();
                return false;
            }
        }

        private static bool TryBuildSapSidHashHeader(out string header)
        {
            header = string.Empty;

            if (!TryReadYouTubeCookies(out var cookies))
            {
                return false;
            }

            if (!cookies.TryGetValue("SAPISID", out var token) && !cookies.TryGetValue("__Secure-3PAPISID", out token))
            {
                LogDebug("SAPISID cookie not found for authorization header.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            const string origin = "https://www.youtube.com";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var input = string.Concat(timestamp, " ", token, " ", origin);
            using var sha1 = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha1.ComputeHash(bytes);
            var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            header = $"{timestamp}_{hash}";
            return true;
        }

        private static bool TryBuildCookieHeader(out string header)
        {
            header = string.Empty;

            if (!TryReadYouTubeCookies(out var cookies))
            {
                return false;
            }

            var parts = cookies
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => $"{kvp.Key}={kvp.Value}")
                .ToList();

            if (parts.Count == 0)
            {
                return false;
            }

            header = string.Join("; ", parts);
            return true;
        }

        private static string? GetDefaultCookiesBrowser()
        {
            try
            {
                var progId = ReadUrlAssociationProgId("http") ?? ReadUrlAssociationProgId("https");
                if (string.IsNullOrWhiteSpace(progId))
                {
                    return null;
                }

                progId = progId.Trim();

                if (progId.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogDebug($"Detected Chrome for cookies (ProgId={progId}).");
                    return "chrome";
                }

                if (progId.IndexOf("Chromium", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogDebug($"Detected Chromium for cookies (ProgId={progId}).");
                    return "chromium";
                }

                if (progId.IndexOf("Edge", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogDebug($"Detected Edge for cookies (ProgId={progId}).");
                    return "edge";
                }

                if (progId.IndexOf("Firefox", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogDebug($"Detected Firefox for cookies (ProgId={progId}).");
                    return "firefox";
                }

                if (progId.IndexOf("Opera", StringComparison.OrdinalIgnoreCase) >= 0 || progId.IndexOf("OPR", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogDebug($"Detected Opera for cookies (ProgId={progId}).");
                    return "opera";
                }

                if (progId.IndexOf("Brave", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogDebug($"Detected Brave for cookies (ProgId={progId}).");
                    return "brave";
                }

                if (progId.IndexOf("Vivaldi", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogDebug($"Detected Vivaldi for cookies (ProgId={progId}).");
                    return "vivaldi";
                }

                LogDebug($"ProgId {progId} did not match a supported browser for cookies.");
                return null;
            }
            catch (Exception ex)
            {
                LogDebug($"GetDefaultCookiesBrowser failed: {ex.Message}");
                return null;
            }
        }

        private string? EnsureYouTubePreview(YouTubeTrack track)
        {
            if (track == null)
            {
                return null;
            }

            var videoId = track.VideoId;
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(track.PreviewPath) && IOFile.Exists(track.PreviewPath))
            {
                return track.PreviewPath;
            }

            var previewFile = Path.Combine(EnsureYouTubeCacheDirectory(), $"{videoId}{YouTubePreviewSuffix}.m4a");
            if (IOFile.Exists(previewFile))
            {
                track.UpdatePreviewPath(previewFile);
                UpdateYouTubeTrackStoragePreviewPath(videoId, previewFile);
                return previewFile;
            }

            var link = ResolveYouTubeLink(track.StreamUrl, track.VideoId);
            if (string.IsNullOrWhiteSpace(link))
            {
                return null;
            }

            if (!PreviewDownloadsInFlight.TryAdd(videoId, 0))
            {
                for (var i = 0; i < 40; i++)
                {
                    Thread.Sleep(250);
                    if (IOFile.Exists(previewFile))
                    {
                        track.UpdatePreviewPath(previewFile);
                        UpdateYouTubeTrackStoragePreviewPath(videoId, previewFile);
                        return previewFile;
                    }
                }

                return null;
            }

            try
            {
                DownloadPreviewSnippet(link, videoId, previewFile);
            }
            finally
            {
                PreviewDownloadsInFlight.TryRemove(videoId, out _);
            }

            if (!IOFile.Exists(previewFile))
            {
                return null;
            }

            track.UpdatePreviewPath(previewFile);
            UpdateYouTubeTrackStoragePreviewPath(videoId, previewFile);
            return previewFile;
        }

        private async Task<string?> EnsureYouTubeBufferAsync(YouTubeTrack track)
        {
            return await Task.Run(() => EnsureYouTubeBuffer(track)).ConfigureAwait(false);
        }

        private string? EnsureYouTubeBuffer(YouTubeTrack track)
        {
            if (track == null)
            {
                return null;
            }

            var videoId = track.VideoId;
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            LogDebug($"EnsureYouTubeBuffer requested for {videoId}");
            if (!string.IsNullOrWhiteSpace(track.BufferPath) && IOFile.Exists(track.BufferPath))
            {
                LogDebug($"EnsureYouTubeBuffer reusing existing buffer {track.BufferPath}");
                return track.BufferPath;
            }

            var bufferFile = Path.Combine(EnsureYouTubeCacheDirectory(), $"{videoId}{YouTubeBufferSuffix}.m4a");
            if (IOFile.Exists(bufferFile))
            {
                LogDebug($"EnsureYouTubeBuffer found cached file {bufferFile}");
                track.UpdateBufferPath(bufferFile);
                UpdateYouTubeTrackStorageBufferPath(videoId, bufferFile);
                return bufferFile;
            }

            var link = ResolveYouTubeLink(track.StreamUrl, track.VideoId);
            if (string.IsNullOrWhiteSpace(link))
            {
                return null;
            }

            if (!BufferDownloadsInFlight.TryAdd(videoId, 0))
            {
                LogDebug($"EnsureYouTubeBuffer detected existing download for {videoId}, waiting");
                for (var i = 0; i < 60; i++)
                {
                    Thread.Sleep(250);
                    if (IOFile.Exists(bufferFile))
                    {
                        track.UpdateBufferPath(bufferFile);
                        return bufferFile;
                    }
                }

                return null;
            }

            try
            {
                LogDebug($"EnsureYouTubeBuffer downloading snippet for {videoId}");
                if (!DownloadBufferSnippet(link, videoId, bufferFile))
                {
                    LogDebug($"EnsureYouTubeBuffer download failed for {videoId}");
                    return null;
                }
            }
            finally
            {
                BufferDownloadsInFlight.TryRemove(videoId, out _);
            }

            if (!IOFile.Exists(bufferFile))
            {
                LogDebug($"EnsureYouTubeBuffer missing file after download for {videoId}");
                return null;
            }

            track.UpdateBufferPath(bufferFile);
            UpdateYouTubeTrackStorageBufferPath(videoId, bufferFile);
            LogDebug($"EnsureYouTubeBuffer completed for {videoId}");
            return bufferFile;
        }

        private async Task EnsureSegmentWindowAsync(YouTubeTrack track, int currentSegmentIndex)
        {
            if (track == null)
            {
                return;
            }

            if (!await EnsureExternalToolsAvailableAsync().ConfigureAwait(false))
            {
                return;
            }

            EnsureSegmentWindowInternal(track, currentSegmentIndex);
        }

        private void EnsureSegmentWindowInternal(YouTubeTrack track, int currentSegmentIndex)
        {
            if (track == null)
            {
                return;
            }

            var videoId = track.VideoId;
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return;
            }

            if (SegmentDownloadDisabledVideos.ContainsKey(videoId))
            {
                LogDebug($"EnsureSegmentWindowInternal skipped: downloads disabled for {videoId}");
                return;
            }

            LogDebug($"EnsureSegmentWindowInternal start for {videoId} index {currentSegmentIndex}");
            var maxIndex = GetMaxSegmentIndex(track);
            if (maxIndex.HasValue)
            {
                currentSegmentIndex = Math.Min(currentSegmentIndex, maxIndex.Value);
            }

            currentSegmentIndex = Math.Max(0, currentSegmentIndex);

            var targets = new HashSet<int>();
            for (var i = 0; i < MaxSegmentFilesPerTrack; i++)
            {
                var index = currentSegmentIndex + i;
                if (maxIndex.HasValue && index > maxIndex.Value)
                {
                    break;
                }

                if (index < 0)
                {
                    continue;
                }

                targets.Add(index);
                var segmentPath = EnsureYouTubeSegment(track, index);
                if (string.IsNullOrWhiteSpace(segmentPath))
                {
                    SegmentDownloadDisabledVideos[videoId] = true;
                    LogDebug($"Segment downloads disabled for {videoId} after failure.");
                    break;
                }
                LogDebug($"EnsureSegmentWindowInternal ensured segment {index} at {segmentPath}");
            }

            if (targets.Count > 0)
            {
                LogDebug($"EnsureSegmentWindowInternal pruning segments for {videoId}, keeping {string.Join(",", targets)}");
                PruneSegmentFiles(videoId, targets);
            }
        }

        private static int? GetMaxSegmentIndex(YouTubeTrack track)
        {
            if (track?.DurationSeconds is null || track.DurationSeconds.Value <= 0)
            {
                return null;
            }

            var totalSeconds = track.DurationSeconds.Value;
            var maxIndex = (int)Math.Floor(Math.Max(0, totalSeconds - 1) / SegmentDurationSeconds);
            return Math.Max(0, maxIndex);
        }

        private string? EnsureYouTubeSegment(YouTubeTrack track, int segmentIndex)
        {
            if (track == null || segmentIndex < 0)
            {
                return null;
            }

            var videoId = track.VideoId;
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            var destination = BuildSegmentFilePath(videoId, segmentIndex);
            if (IOFile.Exists(destination))
            {
                LogDebug($"EnsureYouTubeSegment using existing file {destination}");
                return destination;
            }

            var link = ResolveYouTubeLink(track.StreamUrl, track.VideoId);
            if (string.IsNullOrWhiteSpace(link))
            {
                return null;
            }

            var downloadKey = $"{videoId}_{segmentIndex}";
            if (!SegmentDownloadsInFlight.TryAdd(downloadKey, 0))
            {
                LogDebug($"EnsureYouTubeSegment waiting for in-flight download {downloadKey}");
                while (SegmentDownloadsInFlight.ContainsKey(downloadKey))
                {
                    Thread.Sleep(250);
                    if (IOFile.Exists(destination))
                    {
                        return destination;
                    }
                }

                return IOFile.Exists(destination) ? destination : null;
            }

            try
            {
                SegmentDownloadSemaphore.Wait();
                try
                {
                    LogDebug($"EnsureYouTubeSegment downloading {downloadKey}");
                    if (!DownloadSegmentSnippet(link, videoId, segmentIndex, destination))
                    {
                        LogDebug($"EnsureYouTubeSegment download failed for {downloadKey}");
                        return null;
                    }
                }
                finally
                {
                    SegmentDownloadSemaphore.Release();
                }
            }
            finally
            {
                SegmentDownloadsInFlight.TryRemove(downloadKey, out _);
            }

            return IOFile.Exists(destination) ? destination : null;
        }

        private static string BuildSegmentFilePath(string videoId, int segmentIndex)
        {
            var directory = EnsureYouTubeCacheDirectory();
            var suffix = segmentIndex.ToString("D3", CultureInfo.InvariantCulture);
            return Path.Combine(directory, $"{videoId}{YouTubeSegmentSuffix}{suffix}.m4a");
        }

        private static int? TryParseSegmentIndex(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var markerIndex = name.LastIndexOf(YouTubeSegmentSuffix, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return null;
            }

            var suffix = name.Substring(markerIndex + YouTubeSegmentSuffix.Length);
            return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
        }

        private static void PruneSegmentFiles(string videoId, HashSet<int> allowedIndices)
        {
            try
            {
                var directory = EnsureYouTubeCacheDirectory();
                foreach (var file in Directory.EnumerateFiles(directory, $"{videoId}{YouTubeSegmentSuffix}*.m4a"))
                {
                    var index = TryParseSegmentIndex(file);
                    if (index.HasValue && !allowedIndices.Contains(index.Value))
                    {
                        IOFile.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"PruneSegmentFiles failed for {videoId}: {ex.Message}");
            }
        }

        private static string? ReadUrlAssociationProgId(string scheme)
        {
            try
            {
                var path = $"Software\\Microsoft\\Windows\\Shell\\Associations\\UrlAssociations\\{scheme}\\UserChoice";
                using var key = Registry.CurrentUser.OpenSubKey(path);
                return key?.GetValue("ProgId") as string;
            }
            catch (Exception ex)
            {
                LogDebug($"ReadUrlAssociationProgId failed for {scheme}: {ex.Message}");
                return null;
            }
        }

        private static bool TryRunYtDlp(YtDlpCommand command, out string stdout, out string stderr, Action<double>? progressCallback = null, int? timeoutMilliseconds = null)
        {
            stdout = string.Empty;
            stderr = string.Empty;

            if (!System.IO.File.Exists(ExternalToolInstaller.YtDlpPath))
            {
                LogDebug("TryRunYtDlp: yt-dlp not found.");
                return false;
            }

            var browser = DefaultCookiesBrowser.Value;
            LogDebug($"TryRunYtDlp preparing command for link {command.Link}, cookieFile={command.UsesCookieFile}");
            const int maxAttempts = 3;
            var variantIndex = 0;
            var arguments = command.BuildArgsForVariant(variantIndex);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ExternalToolInstaller.YtDlpPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        WorkingDirectory = Path.GetDirectoryName(ExternalToolInstaller.YtDlpPath) ?? string.Empty
                    };
                    ExternalToolInstaller.ApplyEnvironment(psi);

                    using var process = Process.Start(psi);
                    var sanitizedArgs = SanitizeYtDlpArgs(arguments);
                    LogDebug($"yt-dlp attempt {attempt} (fallback={(variantIndex > 0 ? "yes" : "no")}): {sanitizedArgs}");
                    if (process == null)
                    {
                        LogDebug("TryRunYtDlp: process failed to start.");
                        return false;
                    }

                    var stdoutBuilder = new StringBuilder();
                    var stderrBuilder = new StringBuilder();
                    var stdoutTask = ConsumeYtDlpStreamAsync(process.StandardOutput, stdoutBuilder, parseProgress: progressCallback != null, progressCallback);
                    var stderrTask = ConsumeYtDlpStreamAsync(process.StandardError, stderrBuilder, parseProgress: false, progressCallback: null);

                    var exited = timeoutMilliseconds.HasValue
                        ? process.WaitForExit(timeoutMilliseconds.Value)
                        : process.WaitForExit(-1);

                    if (!exited)
                    {
                        try
                        {
                            process.Kill(entireProcessTree: true);
                        }
                        catch (Exception killEx)
                        {
                            LogDebug($"TryRunYtDlp: failed to kill timed-out process: {killEx.Message}");
                        }

                        LogDebug($"yt-dlp timed out after {timeoutMilliseconds} ms: {sanitizedArgs}");
                        return false;
                    }

                    try
                    {
                        Task.WaitAll(stdoutTask, stderrTask);
                    }
                    catch (AggregateException ex)
                    {
                        LogDebug($"TryRunYtDlp: failed to read yt-dlp streams: {ex.Flatten().InnerException?.Message ?? ex.Message}");
                        return false;
                    }

                    stdout = stdoutBuilder.ToString();
                    stderr = stderrBuilder.ToString();

                    if (process.ExitCode == 0)
                    {
                        LogDebug("yt-dlp completed successfully");
                        return true;
                    }

                    LogDebug($"yt-dlp exited with {process.ExitCode}: {stderr}");

                    if (command.HasFallback && ShouldApply403Fallback(stderr) && command.TryAdvanceVariant(ref variantIndex, out arguments, out var variantDescription))
                    {
                        var fallbackMessage = variantDescription ?? (command.UsesCookieFile
                            ? "Applying yt-dlp fallback (authenticated cookies)."
                            : "Applying yt-dlp fallback (android client).");
                        LogDebug(fallbackMessage);
                        Thread.Sleep(500);
                        attempt = 0; // reset attempt counter for new variant
                        continue;
                    }

                    if (!command.UsesCookieFile && HandleCookieLockFailure(browser, stderr) && attempt < maxAttempts)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    LogDebug($"TryRunYtDlp exception: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        private static async Task ConsumeYtDlpStreamAsync(StreamReader reader, StringBuilder buffer, bool parseProgress, Action<double>? progressCallback)
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    buffer.AppendLine(line);
                    if (parseProgress)
                    {
                        TryReportYtDlpProgress(line, progressCallback);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"ConsumeYtDlpStreamAsync exception: {ex.Message}");
            }
        }

        private static void TryReportYtDlpProgress(string line, Action<double>? progressCallback)
        {
            if (progressCallback == null || string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (line.IndexOf("[download]", StringComparison.OrdinalIgnoreCase) < 0 &&
                line.IndexOf("[extractaudio]", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            var match = YtDlpProgressRegex.Match(line);
            if (!match.Success)
            {
                return;
            }

            if (!double.TryParse(match.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                return;
            }

            var clamped = Math.Clamp(percent, 0d, 100d);
            try
            {
                progressCallback(clamped);
            }
            catch (Exception ex)
            {
                LogDebug($"TryReportYtDlpProgress callback exception: {ex.Message}");
            }
        }

        private static string SanitizeYtDlpArgs(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return arguments;
            }

            const string marker = "SAPISIDHASH ";
            var index = arguments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return arguments;
            }

            var start = index + marker.Length;
            var end = arguments.IndexOf('"', start);
            if (end < 0)
            {
                end = arguments.Length;
            }

            return string.Concat(arguments.AsSpan(0, start), "***", arguments.AsSpan(end));
        }

        private static bool ShouldApply403Fallback(string stderr)
        {
            if (string.IsNullOrWhiteSpace(stderr))
            {
                return false;
            }

            return stderr.IndexOf("HTTP Error 403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   stderr.IndexOf("returns code 403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   stderr.IndexOf("Sign in to confirm you're not a bot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   stderr.IndexOf("Skipping unsupported client", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HandleCookieLockFailure(string? browser, string stderr)
        {
            if (string.IsNullOrWhiteSpace(browser) || string.IsNullOrWhiteSpace(stderr))
            {
                return false;
            }

            if (stderr.IndexOf("Could not copy Chrome cookie database", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            LogDebug($"Detected cookie database lock for browser {browser}. Prompting user to close browser.");
            return RequestBrowserClosure(browser);
        }

        private static bool RequestBrowserClosure(string browser)
        {
            var displayName = GetBrowserDisplayName(browser);
            MessageBoxResult result = MessageBoxResult.Cancel;

            void ShowPrompt()
            {
                result = MessageBox.Show(
                    $"Мы закроем ваш браузер {displayName}, чтобы взять куки для программы. Продолжить?",
                    "Подготовка YouTube трека",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
            }

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(ShowPrompt);
            }
            else
            {
                ShowPrompt();
            }

            if (result != MessageBoxResult.OK)
            {
                LogDebug("User cancelled browser closure prompt.");
                return false;
            }

            var closed = CloseBrowserProcesses(browser);
            if (!closed)
            {
                LogDebug($"No running processes found for browser {browser} to close.");
            }

            Thread.Sleep(1000);
            return true;
        }

        private static bool CloseBrowserProcesses(string browser)
        {
            var processNames = GetBrowserProcessNames(browser);
            if (processNames.Count == 0)
            {
                return false;
            }

            var closedAny = false;

            foreach (var name in processNames)
            {
                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(name);
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to enumerate processes for {name}: {ex.Message}");
                    continue;
                }

                foreach (var process in processes)
                {
                    try
                    {
                        closedAny = true;
                        if (process.CloseMainWindow())
                        {
                            if (process.WaitForExit(3000))
                            {
                                continue;
                            }
                        }

                        process.Kill();
                        process.WaitForExit(2000);
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Failed to close browser process {name} (PID {process.Id}): {ex.Message}");
                    }
                }
            }

            return closedAny;
        }

        private static IReadOnlyList<string> GetBrowserProcessNames(string browser) => browser.ToLowerInvariant() switch
        {
            "chrome" => new[] { "chrome" },
            "chromium" => new[] { "chromium" },
            "edge" => new[] { "msedge" },
            "firefox" => new[] { "firefox" },
            "opera" => new[] { "opera" },
            "brave" => new[] { "brave" },
            "vivaldi" => new[] { "vivaldi" },
            _ => Array.Empty<string>()
        };

        private static string GetBrowserDisplayName(string browser) => browser.ToLowerInvariant() switch
        {
            "chrome" => "Chrome",
            "chromium" => "Chromium",
            "edge" => "Microsoft Edge",
            "firefox" => "Firefox",
            "opera" => "Opera",
            "brave" => "Brave",
            "vivaldi" => "Vivaldi",
            _ => browser
        };

        public MusicMiniAppWindow()
        {
            InitializeComponent();

            UiScaleManager.RegisterWindow(this);

            WindowSettingsManager.RegisterWindow(this, WindowKey);
            ExternalToolInstaller.Logger = OnExternalToolLog;
            Closed += OnWindowClosed;
            Loaded += MusicMiniAppWindow_OnLoaded;
            _volumeSaveTimer.Tick += VolumeSaveTimerOnTick;

            UpdateYouTubeCookiesIndicator();
            CleanupYouTubeCacheOnStartup();

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            LogDebug($"MusicMiniAppWindow initialized. BaseDirectory='{baseDir}'");
        }

        private bool _youTubeWebViewInitialized;

        private async void MusicMiniAppWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeYouTubeWebViewAsync();
        }

        private async Task InitializeYouTubeWebViewAsync()
        {
            if (_youTubeWebViewInitialized || YouTubeWebView == null)
            {
                return;
            }

            try
            {
                var userDataFolder = EnsureYouTubeWebViewUserDataFolder();
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);
                YouTubeWebView.CoreWebView2InitializationCompleted += YouTubeWebView_OnCoreWebView2InitializationCompleted;
                await YouTubeWebView.EnsureCoreWebView2Async(environment);
                _youTubeWebViewInitialized = true;

                await ConfigureYouTubeWebViewAsync();
                NavigateYouTubeWebViewHome();
            }
            catch (Exception ex)
            {
                LogDebug($"InitializeYouTubeWebViewAsync failed: {ex.Message}");
                HandleYouTubeWebViewInitializationFailure(ex);
            }
        }

        private async Task ConfigureYouTubeWebViewAsync()
        {
            if (YouTubeWebView?.CoreWebView2 == null)
            {
                return;
            }

            YouTubeWebView.DefaultBackgroundColor = System.Drawing.Color.Black;
            var settings = YouTubeWebView.CoreWebView2.Settings;
            settings.AreDefaultScriptDialogsEnabled = true;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = true;

            YouTubeWebView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                LogDebug($"YouTubeWebView navigation starting: {args.Uri}");
            };
            YouTubeWebView.CoreWebView2.ProcessFailed += (_, args) =>
            {
                LogDebug($"YouTubeWebView process failed (Reason={args.Reason})");
            };

            RegisterYouTubeAdBlocker(YouTubeWebView.CoreWebView2);
            await EnsureYouTubeAdBlockerScriptRegisteredAsync(YouTubeWebView.CoreWebView2);
        }

        private void RegisterYouTubeAdBlocker(CoreWebView2 webView2)
        {
            if (_youTubeAdBlockConfigured || webView2 == null)
            {
                return;
            }

            foreach (var pattern in YouTubeAdBlockPatterns)
            {
                try
                {
                    webView2.AddWebResourceRequestedFilter(pattern, CoreWebView2WebResourceContext.All);
                }
                catch (Exception ex)
                {
                    LogDebug($"AddWebResourceRequestedFilter failed for pattern {pattern}: {ex.Message}");
                }
            }

            webView2.WebResourceRequested += YouTubeWebView_OnWebResourceRequested;
            _youTubeAdBlockConfigured = true;
        }

        private void YouTubeWebView_OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var uri = e.Request?.Uri;
            if (string.IsNullOrWhiteSpace(uri))
            {
                return;
            }

            foreach (var fragment in YouTubeAdUrlFragments)
            {
                if (uri.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                try
                {
                    LogDebug($"Blocking YouTube ad request: {uri}");
                    var environment = YouTubeWebView?.CoreWebView2?.Environment ?? (sender as CoreWebView2)?.Environment;
                    var response = environment?.CreateWebResourceResponse(Stream.Null, 204, "NoContent", "");
                    if (response != null)
                    {
                        e.Response = response;
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to block ad request for {uri}: {ex.Message}");
                }

                return;
            }
        }

        private void NavigateYouTubeWebViewHome(bool forceReload = false)
        {
            if (YouTubeWebView?.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                if (forceReload && YouTubeWebView.Source != null && YouTubeWebView.Source == YouTubeHomeUri)
                {
                    LogDebug("YouTubeWebView reload requested.");
                    YouTubeWebView.Reload();
                    return;
                }

                var target = YouTubeHomeUri.AbsoluteUri;
                LogDebug($"YouTubeWebView navigating to {target}");
                YouTubeWebView.CoreWebView2.Navigate(target);
            }
            catch (Exception ex)
            {
                LogDebug($"NavigateYouTubeWebViewHome failed: {ex.Message}");
            }
        }

        private void YouTubeWebView_OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                LogDebug("YouTubeWebView initialization completed successfully.");
                return;
            }

            var message = e.InitializationException?.Message ?? "Unknown error";
            LogDebug($"YouTubeWebView initialization failed: {message}");
            HandleYouTubeWebViewInitializationFailure(e.InitializationException ?? new InvalidOperationException(message));
        }

        private string EnsureYouTubeWebViewUserDataFolder()
        {
            try
            {
                Directory.CreateDirectory(YouTubeWebViewUserDataFolder);
            }
            catch (Exception ex)
            {
                LogDebug($"EnsureYouTubeWebViewUserDataFolder failed: {ex.Message}");
            }

            return YouTubeWebViewUserDataFolder;
        }

        private void HandleYouTubeWebViewInitializationFailure(Exception? exception)
        {
            if (_youTubeWebViewInitializationFailed)
            {
                return;
            }

            _youTubeWebViewInitializationFailed = true;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (YouTubeLegacyPanel != null)
                    {
                        YouTubeLegacyPanel.Visibility = Visibility.Visible;
                    }

                    if (YouTubeWebView != null)
                    {
                        YouTubeWebView.Visibility = Visibility.Collapsed;
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"HandleYouTubeWebViewInitializationFailure UI update failed: {ex.Message}");
                }
            });

            var runtimeMissing = exception is WebView2RuntimeNotFoundException;
            var builder = new StringBuilder();
            builder.AppendLine("Не удалось инициализировать встроенный браузер YouTube.");
            if (runtimeMissing)
            {
                builder.AppendLine("На компьютере не установлен Microsoft Edge WebView2 Runtime.");
                builder.AppendLine("Открыть страницу загрузки?");
            }
            else if (exception != null)
            {
                builder.AppendLine($"Ошибка: {exception.Message}");
            }

            Dispatcher.Invoke(() =>
            {
                try
                {
                    var buttons = runtimeMissing ? MessageBoxButton.YesNo : MessageBoxButton.OK;
                    var result = MessageBox.Show(this,
                        builder.ToString(),
                        "YouTube",
                        buttons,
                        MessageBoxImage.Warning);

                    if (runtimeMissing && result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = WebView2RuntimeDownloadUrl,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception openEx)
                        {
                            LogDebug($"Failed to open WebView2 download page: {openEx.Message}");
                        }
                    }
                }
                catch (Exception dialogEx)
                {
                    LogDebug($"HandleYouTubeWebViewInitializationFailure dialog failed: {dialogEx.Message}");
                }
            });
        }

        private void YouTubeWebView_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LogDebug(e.IsSuccess
                ? $"YouTubeWebView navigation completed: {YouTubeWebView?.Source}"
                : $"YouTubeWebView navigation failed (ErrorStatus={e.WebErrorStatus})");

            if (e.IsSuccess)
            {
                _ = InjectYouTubeAdBlockerScriptAsync();
            }
        }

        private async Task InjectYouTubeAdBlockerScriptAsync()
        {
            if (YouTubeWebView?.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                await YouTubeWebView.ExecuteScriptAsync(YouTubeAdBlockerScript);
                LogDebug("YouTube ad-block script executed on current document.");
            }
            catch (Exception ex)
            {
                LogDebug($"InjectYouTubeAdBlockerScriptAsync failed: {ex.Message}");
            }
        }

        private async Task EnsureYouTubeAdBlockerScriptRegisteredAsync(CoreWebView2 webView2)
        {
            if (_youTubeAdScriptInjected || webView2 == null)
            {
                return;
            }

            try
            {
                await webView2.AddScriptToExecuteOnDocumentCreatedAsync(YouTubeAdBlockerScript);
                _youTubeAdScriptInjected = true;
                LogDebug("YouTube ad-block script registered for all documents.");
            }
            catch (Exception ex)
            {
                LogDebug($"EnsureYouTubeAdBlockerScriptRegisteredAsync failed: {ex.Message}");
            }
        }

        private void CleanupYouTubeCacheOnStartup()
        {
            try
            {
                if (!Directory.Exists(YouTubeCacheDirectory))
                {
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(YouTubeCacheDirectory))
                {
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
                    {
                        continue;
                    }

                    if (!fileNameWithoutExtension.EndsWith(YouTubePreviewSuffix, StringComparison.OrdinalIgnoreCase) &&
                        !fileNameWithoutExtension.Contains(YouTubeSegmentSuffix, StringComparison.OrdinalIgnoreCase) &&
                        !fileNameWithoutExtension.EndsWith(YouTubeBufferSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        IOFile.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"CleanupYouTubeCacheOnStartup failed: {ex.Message}");
            }
        }

        private async Task<bool> EnsureExternalToolsAvailableAsync()
        {
            if (_externalToolsReady)
            {
                return true;
            }

            if (_toolInstallTask != null)
            {
                return await _toolInstallTask.ConfigureAwait(false);
            }

            _toolInstallCts = new CancellationTokenSource();
            var token = _toolInstallCts.Token;

            ShowToolInstallOverlay("Загрузка зависимостей...");

            _toolInstallTask = RunToolInstallAsync(token);

            var ready = await _toolInstallTask.ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                HideToolInstallOverlay();
            });

            _toolInstallTask = null;
            _toolInstallCts = null;

            _externalToolsReady = ready;
            return ready;
        }

        private async Task<bool> RunToolInstallAsync(CancellationToken token)
        {
            try
            {
                var ready = await ExternalToolInstaller.EnsureToolsAsync(token).ConfigureAwait(false);
                return ready;
            }
            catch (OperationCanceledException)
            {
                LogDebug("Tool installation cancelled by user");
                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"Tool installation failed: {ex.Message}");
                return false;
            }
        }

        private void ShowToolInstallOverlay(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ToolInstallMessageTextBlock.Text = message;
                ToolInstallOverlay.Visibility = Visibility.Visible;
            });
        }

        private void HideToolInstallOverlay()
        {
            ToolInstallOverlay.Visibility = Visibility.Collapsed;
        }

        private void UpdateToolInstallMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ToolInstallMessageTextBlock.Text = message;
            });
        }

        private void ToolInstallCancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_toolInstallCts == null)
            {
                HideToolInstallOverlay();
                return;
            }

            UpdateToolInstallMessage("Отмена...");
            _toolInstallCts.Cancel();
        }

        private void OnExternalToolLog(string message)
        {
            LogDebug(message);
            if (ToolInstallOverlay.Visibility == Visibility.Visible)
            {
                UpdateToolInstallMessage(message);
            }
        }

        private void PositionTimerOnTick(object? sender, EventArgs e)
        {
            if (!_deck1UserSeeking)
            {
                UpdateSeekSliderForDeck(_deck1, Deck1SeekSlider);
                UpdateDeckResumePosition(_deck1, Deck1SeekSlider);
            }
            MaintainSegmentWindowForDeck(_deck1);
            if (!_deck2UserSeeking)
            {
                UpdateSeekSliderForDeck(_deck2, Deck2SeekSlider);
                UpdateDeckResumePosition(_deck2, Deck2SeekSlider);
            }
            MaintainSegmentWindowForDeck(_deck2);
            if (!_deck3UserSeeking)
            {
                UpdateSeekSliderForDeck(_deck3, Deck3SeekSlider);
                UpdateDeckResumePosition(_deck3, Deck3SeekSlider);
            }
                MaintainSegmentWindowForDeck(_deck3);
            if (!_deck4UserSeeking)
            {
                UpdateSeekSliderForDeck(_deck4, Deck4SeekSlider);
                UpdateDeckResumePosition(_deck4, Deck4SeekSlider);
            }
            MaintainSegmentWindowForDeck(_deck4);
        }

        private void MaintainSegmentWindowForDeck(DeckState deck)
        {
            if (deck == null || deck.Queue.Count == 0 || deck.CurrentIndex < 0)
            {
                return;
            }

            var item = deck.Queue[deck.CurrentIndex];
            if (item == null || string.IsNullOrWhiteSpace(item.VideoId))
            {
                return;
            }

            if (_segmentUpdateDebounce.TryGetValue(item.VideoId, out var lastTick) &&
                (DateTime.UtcNow - lastTick) < SegmentUpdateThrottle)
            {
                return;
            }

            _segmentUpdateDebounce[item.VideoId] = DateTime.UtcNow;

            var track = item.SourceYouTubeTrack ?? _youTubeTracks.FirstOrDefault(t =>
                string.Equals(t.VideoId, item.VideoId, StringComparison.OrdinalIgnoreCase));
            if (track == null)
            {
                return;
            }

            var seconds = GetDeckPlaybackPositionSeconds(deck);
            var segmentIndex = Math.Max(0, (int)Math.Floor(seconds / SegmentDurationSeconds));
            _ = Task.Run(() => EnsureSegmentWindowAsync(track, segmentIndex));
        }

        private void UpdateSeekSliderForDeck(DeckState deck, Slider slider)
        {
            var timeLabel = GetSeekTimeLabel(deck);

            if (deck.UseVlc)
            {
                if (deck.VlcPlayer == null)
                {
                    return;
                }

                var totalMs = deck.VlcPlayer.Length;
                var currentMs = deck.VlcPlayer.Time;
                if (totalMs <= 0)
                {
                    totalMs = 1;
                }
                currentMs = Math.Max(0, Math.Min(totalMs, currentMs));
                var totalSeconds = totalMs / 1000.0;
                var currentSeconds = currentMs / 1000.0;
                SafeUpdateSlider(slider, 0, totalSeconds <= 0 ? 1.0 : totalSeconds, currentSeconds);
                UpdateSeekTimeLabel(currentSeconds, timeLabel);
                return;
            }

            if (deck.Stream == null)
            {
                return;
            }

            var total = GetTotalSeconds(deck.Stream);
            var current = deck.Stream.CurrentTime.TotalSeconds;
            if (double.IsNaN(total) || double.IsInfinity(total) || total <= 0)
            {
                total = 1.0;
            }
            current = Math.Max(0, Math.Min(total, current));
            SafeUpdateSlider(slider, 0, total, current);
            UpdateSeekTimeLabel(current, timeLabel);
        }

        private void UpdateDeckResumePosition(DeckState deck, Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            deck.ResumePosition = GetSliderPosition(slider);

            if (deck.UseVlc)
            {
                deck.IsPlaying = deck.VlcPlayer != null && deck.VlcPlayer.IsPlaying;
            }
            else if (deck.WaveOut != null)
            {
                deck.IsPlaying = deck.WaveOut.PlaybackState == PlaybackState.Playing;
            }
            else
            {
                deck.IsPlaying = false;
            }
        }

        private static double GetSliderPosition(Slider slider)
        {
            var value = slider.Value;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                value = 0;
            }

            var minimum = slider.Minimum;
            if (double.IsNaN(minimum) || double.IsInfinity(minimum))
            {
                minimum = 0;
            }

            var maximum = slider.Maximum;
            if (double.IsNaN(maximum) || double.IsInfinity(maximum))
            {
                maximum = minimum + 1;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double GetDeckPlaybackPositionSeconds(DeckState deck)
        {
            if (deck == null)
            {
                return 0;
            }

            if (deck.UseVlc && deck.VlcPlayer != null)
            {
                var timeMs = deck.VlcPlayer.Time;
                if (timeMs < 0)
                {
                    timeMs = 0;
                }

                return timeMs / 1000.0;
            }

            if (deck.Stream != null)
            {
                var current = deck.Stream.CurrentTime.TotalSeconds;
                var total = GetTotalSeconds(deck.Stream);
                if (double.IsNaN(current) || double.IsInfinity(current) || current < 0)
                {
                    current = 0;
                }

                if (!double.IsNaN(total) && !double.IsInfinity(total) && total > 0)
                {
                    current = Math.Min(current, total);
                }

                return current;
            }

            return Math.Max(0, deck.ResumePosition);
        }

        private string GetDeckName(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return "Deck1";
            if (ReferenceEquals(deck, _deck2)) return "Deck2";
            if (ReferenceEquals(deck, _deck3)) return "Deck3";
            return "Deck4";
        }

        private Slider GetSeekSlider(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return Deck1SeekSlider;
            if (ReferenceEquals(deck, _deck2)) return Deck2SeekSlider;
            if (ReferenceEquals(deck, _deck3)) return Deck3SeekSlider;
            return Deck4SeekSlider;
        }

        private TextBlock GetSeekTimeLabel(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return Deck1SeekTimeLabel;
            if (ReferenceEquals(deck, _deck2)) return Deck2SeekTimeLabel;
            if (ReferenceEquals(deck, _deck3)) return Deck3SeekTimeLabel;
            return Deck4SeekTimeLabel;
        }

        private ToggleButton GetPlayToggle(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return Deck1PlayPauseToggle;
            if (ReferenceEquals(deck, _deck2)) return Deck2PlayPauseToggle;
            if (ReferenceEquals(deck, _deck3)) return Deck3PlayPauseToggle;
            return Deck4PlayPauseToggle;
        }

        private Slider GetVolumeSlider(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return Deck1VolumeSlider;
            if (ReferenceEquals(deck, _deck2)) return Deck2VolumeSlider;
            if (ReferenceEquals(deck, _deck3)) return Deck3VolumeSlider;
            return Deck4VolumeSlider;
        }

        private string GetVolumeSettingKey(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return "Deck1Volume";
            if (ReferenceEquals(deck, _deck2)) return "Deck2Volume";
            if (ReferenceEquals(deck, _deck3)) return "Deck3Volume";
            return "Deck4Volume";
        }

        private void CancelDeckFade(DeckState deck)
        {
            if (deck.FadeCancellation != null)
            {
                try
                {
                    deck.FadeCancellation.Cancel();
                }
                catch
                {
                    // ignore cancellation race
                }
            }
        }

        private TextBlock GetNowLabel(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return Deck1NowLabel;
            if (ReferenceEquals(deck, _deck2)) return Deck2NowLabel;
            if (ReferenceEquals(deck, _deck3)) return Deck3NowLabel;
            return Deck4NowLabel;
        }

        private ComboBox GetDeviceCombo(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return Deck1DeviceCombo;
            if (ReferenceEquals(deck, _deck2)) return Deck2DeviceCombo;
            if (ReferenceEquals(deck, _deck3)) return Deck3DeviceCombo;
            return Deck4DeviceCombo;
        }

        private EventHandler<StoppedEventArgs> GetPlaybackStoppedHandler(DeckState deck)
        {
            if (ReferenceEquals(deck, _deck1)) return OnDeck1PlaybackStopped;
            if (ReferenceEquals(deck, _deck2)) return OnDeck2PlaybackStopped;
            if (ReferenceEquals(deck, _deck3)) return OnDeck3PlaybackStopped;
            return OnDeck4PlaybackStopped;
        }

        private void SetPlayToggleState(ToggleButton toggle, bool isPlaying)
        {
            if (toggle.IsChecked == isPlaying)
            {
                toggle.Content = isPlaying ? "⏸" : "⏵";
                return;
            }

            try
            {
                _suppressToggleHandlers = true;
                toggle.IsChecked = isPlaying;
                toggle.Content = isPlaying ? "⏸" : "⏵";
            }
            finally
            {
                _suppressToggleHandlers = false;
            }
        }

        private void SafeUpdateSlider(Slider slider, double min, double max, double value)
        {
            try
            {
                _isUpdatingSliders = true;
                slider.Minimum = min;
                slider.Maximum = Math.Max(1, max);
                slider.Value = Math.Max(min, Math.Min(max, value));
            }
            finally
            {
                _isUpdatingSliders = false;
            }
        }

        private static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            {
                seconds = 0;
            }
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
                : $"{ts.Minutes}:{ts.Seconds:00}";
        }

        private void UpdateSeekTimeLabel(double seconds, TextBlock label)
        {
            label.Text = FormatTime(seconds);
        }

        private static double GetTotalSeconds(WaveStream stream)
        {
            var total = stream.TotalTime.TotalSeconds;
            if (double.IsInfinity(total) || double.IsNaN(total) || total <= 0)
            {
                try
                {
                    var bytesPerSec = stream.WaveFormat.AverageBytesPerSecond;
                    if (bytesPerSec > 0)
                    {
                        total = (double)stream.Length / bytesPerSec;
                    }
                }
                catch
                {
                    total = 0;
                }
            }
            return total;
        }

        private void Deck1SeekSlider_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _deck1UserSeeking = true;
        }

        private void Deck1SeekSlider_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _deck1UserSeeking = false;
            ApplySeek(_deck1, Deck1SeekSlider);
        }

        private void Deck1SeekSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSliders)
            {
                return;
            }
            if (_deck1UserSeeking)
            {
                ApplySeek(_deck1, Deck1SeekSlider);
            }
            UpdateSeekTimeLabel(Deck1SeekSlider.Value, Deck1SeekTimeLabel);
        }

        private void Deck2SeekSlider_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _deck2UserSeeking = true;
        }

        private void Deck2SeekSlider_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _deck2UserSeeking = false;
            ApplySeek(_deck2, Deck2SeekSlider);
        }

        private void Deck2SeekSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSliders)
            {
                return;
            }
            if (_deck2UserSeeking)
            {
                ApplySeek(_deck2, Deck2SeekSlider);
            }
            UpdateSeekTimeLabel(Deck2SeekSlider.Value, Deck2SeekTimeLabel);
        }

        private void Deck3SeekSlider_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _deck3UserSeeking = true;
        }

        private void Deck3SeekSlider_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _deck3UserSeeking = false;
            ApplySeek(_deck3, Deck3SeekSlider);
        }

        private void Deck3SeekSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSliders)
            {
                return;
            }
            if (_deck3UserSeeking)
            {
                ApplySeek(_deck3, Deck3SeekSlider);
            }
            UpdateSeekTimeLabel(Deck3SeekSlider.Value, Deck3SeekTimeLabel);
        }

        private void Deck4SeekSlider_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _deck4UserSeeking = true;
        }

        private void Deck4SeekSlider_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _deck4UserSeeking = false;
            ApplySeek(_deck4, Deck4SeekSlider);
        }

        private void Deck4SeekSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSliders)
            {
                return;
            }
            if (_deck4UserSeeking)
            {
                ApplySeek(_deck4, Deck4SeekSlider);
            }
            UpdateSeekTimeLabel(Deck4SeekSlider.Value, Deck4SeekTimeLabel);
        }

        private void ApplySeek(DeckState deck, Slider slider)
        {
            if (deck == null || slider == null)
            {
                return;
            }

            var label = GetNowLabel(deck);
            var targetSeconds = Math.Max(slider.Minimum, Math.Min(slider.Maximum, slider.Value));
            SeekDeck(deck, targetSeconds, label);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            WindowResizeHelper.EnableResize(this, disableSystemResize: true);
            TopmostToggleBinder.Bind(this, MiniAppTopmostToggle);

            PlaylistListBox.ItemsSource = _playlist;
            YouTubeListBox.ItemsSource = _youTubeTracks;

            EnsurePlaylistTabContentHost(DownloadedTabItem);

            _musicDirectory = EnsureDirectoryExists(DefaultMusicDirectory);
            EnsureDirectoryExists(BackgroundMusicDirectory);
            _meta = MusicDataManager.LoadAll();

            var vol1 = GetSavedVolume("Deck1Volume", 0.6);
            var vol2 = GetSavedVolume("Deck2Volume", 0.6);
            Deck1VolumeSlider.Value = vol1;
            Deck2VolumeSlider.Value = vol2;
            _deck1.Volume = vol1;
            _deck2.Volume = vol2;

            LoadPlaylist();
            ApplyFilter();

            LoadYouTubeTracks();
            ApplyYouTubeFilter();

            _positionTimer.Tick += PositionTimerOnTick;
            _positionTimer.Start();

            InitAudioDevices();

            _playlistRefreshTimer.Tick += PlaylistRefreshTimerOnTick;

            // capture default column widths and min widths for collapse/expand
            try
            {
                var cols = MainColumnsGrid.ColumnDefinitions;
                _leftDefaultWidth = cols[0].Width;
                _leftDefaultMinWidth = cols[0].MinWidth;
                _leftSavedWidth = _leftDefaultWidth;

                _splitterDefaultWidth = cols[1].Width;
                _splitterSavedWidth = _splitterDefaultWidth;

                if (cols.Count > 2)
                {
                    _rightDefaultWidth = cols[2].Width;
                    _rightDefaultMinWidth = cols[2].MinWidth;
                    _rightSavedWidth = _rightDefaultWidth;
                }

                if (_leftDefaultWidth.IsStar && _rightDefaultWidth.IsStar)
                {
                    var totalStars = _leftDefaultWidth.Value + _rightDefaultWidth.Value;
                    _lastLeftRatio = totalStars > 0 ? _leftDefaultWidth.Value / totalStars : 0.3;
                }
                else
                {
                    _lastLeftRatio = 0.3;
                }

                // Restore saved splitter position for left column (in pixels)
                if (WindowSettingsManager.TryGetDouble(WindowKey, "LeftColumnWidth", out var savedLeftWidth))
                {
                    var leftCol = cols[0];
                    if (savedLeftWidth <= 0)
                    {
                        leftCol.MinWidth = 0;
                        leftCol.Width = new GridLength(0);
                        _leftCollapsed = true;
                    }
                    else
                    {
                        // Ensure MinWidth does not block restoration of a narrower saved width
                        leftCol.MinWidth = Math.Min(_leftDefaultMinWidth, savedLeftWidth);
                        leftCol.Width = new GridLength(savedLeftWidth);
                        _leftCollapsed = leftCol.Width.Value <= 0.1;
                    }
                }
            }
            catch
            {
                // ignore, grid may not be ready in designer
            }

            // Reapply saved splitter width after layout is ready
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var cols = MainColumnsGrid.ColumnDefinitions;
                        // Prefer ratio: set star units to avoid later star layout overrides
                        if (WindowSettingsManager.TryGetDouble(WindowKey, "LeftColumnRatio", out var savedRatio) && savedRatio >= 0)
                        {
                            savedRatio = Math.Clamp(savedRatio, 0.0, 0.99);
                            var rightCol = cols[2];
                            double rightStar = rightCol.Width.IsStar ? rightCol.Width.Value : 8.0;
                            if (!rightCol.Width.IsStar)
                            {
                                rightCol.Width = new GridLength(rightStar, GridUnitType.Star);
                            }
                            var leftStar = rightStar * (savedRatio / Math.Max(0.0001, 1.0 - savedRatio));
                            var leftCol = cols[0];
                            leftCol.MinWidth = _leftDefaultMinWidth;
                            leftCol.Width = new GridLength(Math.Max(0.01, leftStar), GridUnitType.Star);
                            _leftCollapsed = leftStar <= 0.01;
                            _lastLeftRatio = savedRatio;
                            _lastLeftRatio = savedRatio;
                        }
                        else if (WindowSettingsManager.TryGetDouble(WindowKey, "LeftColumnWidth", out var savedLeftWidth))
                        {
                            var leftCol = cols[0];
                            if (savedLeftWidth <= 0)
                            {
                                leftCol.MinWidth = 0;
                                leftCol.Width = new GridLength(0);
                                _leftCollapsed = true;
                            }
                            else
                            {
                                leftCol.MinWidth = Math.Min(_leftDefaultMinWidth, savedLeftWidth);
                                leftCol.Width = new GridLength(savedLeftWidth);
                                _leftCollapsed = false;
                                _lastLeftRatio = Math.Clamp(savedLeftWidth / Math.Max(1.0, MainColumnsGrid.ActualWidth), 0.01, 0.99);
                            }
                        }
                    }
                    catch { }
                }), DispatcherPriority.Loaded);
            }
            catch { }

            UpdateDeckColumnVisibility(IsLocalPlaylistTab(TrackSourcesTabControl.SelectedItem));
        }

        private void Window_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.OriginalSource is not DependencyObject source)
                {
                    return;
                }

                var description = BuildElementDescription(source);
                if (string.Equals(description, _lastHoverElementDescription, StringComparison.Ordinal))
                {
                    return;
                }

                _lastHoverElementDescription = description;
                LogDebug($"Hover element: {description}");
            }
            catch
            {
                // ignore hover logging issues
            }
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

        private void MainColumnsGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (_rightCollapsed)
                {
                    return;
                }

                // When window resizes, if columns ended up in absolute pixels (not star), convert to star using current ratio.
                var cols = MainColumnsGrid.ColumnDefinitions;
                if (cols.Count >= 3)
                {
                    var left = cols[0];
                    var splitter = cols[1];
                    var right = cols[2];

                    if (!left.Width.IsStar || !right.Width.IsStar)
                    {
                        var total = Math.Max(1.0, MainColumnsGrid.ActualWidth - splitter.ActualWidth);
                        var ratio = total > 0 ? Math.Max(0.0, Math.Min(1.0, left.ActualWidth / total)) : 0.5;
                        SetStarColumnsByRatio(ratio);
                        WindowSettingsManager.SetDouble(WindowKey, "LeftColumnRatio", ratio);
                        WindowSettingsManager.SetDouble(WindowKey, "LeftColumnWidth", left.ActualWidth);
                        _lastLeftRatio = ratio;
                    }
                }
            }
            catch { }
        }

        private void UpdateDeckColumnVisibility(bool showDeckColumn)
        {
            if (DeckColumnScrollViewer == null || MainGridSplitter == null || LeftSplitterToggleButton == null)
            {
                return;
            }

            var cols = MainColumnsGrid.ColumnDefinitions;
            if (cols.Count < 3)
            {
                DeckColumnScrollViewer.Visibility = showDeckColumn ? Visibility.Visible : Visibility.Collapsed;
                MainGridSplitter.Visibility = showDeckColumn ? Visibility.Visible : Visibility.Collapsed;
                LeftSplitterToggleButton.Visibility = showDeckColumn ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            var left = cols[0];
            var splitterCol = cols[1];
            var right = cols[2];

            if (showDeckColumn)
            {
                DeckColumnScrollViewer.Visibility = Visibility.Visible;
                MainGridSplitter.Visibility = Visibility.Visible;
                MainGridSplitter.IsEnabled = true;
                LeftSplitterToggleButton.Visibility = Visibility.Visible;

                splitterCol.Width = _splitterSavedWidth.Value > 0 ? _splitterSavedWidth : _splitterDefaultWidth;
                splitterCol.MinWidth = 0;

                right.MinWidth = _rightDefaultMinWidth;
                if (_rightSavedWidth.Value <= 0)
                {
                    _rightSavedWidth = _rightDefaultWidth.Value > 0 ? _rightDefaultWidth : new GridLength(1.0, GridUnitType.Star);
                }
                right.Width = _rightSavedWidth;
                _rightCollapsed = right.Width.Value <= 0.1;

                var ratio = _lastLeftRatio > 0 ? _lastLeftRatio : 0.3;
                SetStarColumnsByRatio(ratio);
            }
            else
            {
                _rightSavedWidth = right.Width;
                _splitterSavedWidth = splitterCol.Width;

                DeckColumnScrollViewer.Visibility = Visibility.Collapsed;
                right.MinWidth = 0;
                right.Width = new GridLength(0);
                _rightCollapsed = true;

                splitterCol.Width = new GridLength(0);
                MainGridSplitter.Visibility = Visibility.Collapsed;
                MainGridSplitter.IsEnabled = false;
                LeftSplitterToggleButton.Visibility = Visibility.Collapsed;

                left.MinWidth = 0;
                left.Width = new GridLength(1.0, GridUnitType.Star);
            }
        }

        private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new AppSettingsWindow
                {
                    Owner = this
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Не удалось открыть окно настроек.\n{ex.Message}",
                    "Настройки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            // Persist splitter position one last time
            SaveLeftSplitterPosition();

            FlushPendingVolumeSaves();

            StopDeck(_deck1);
            StopDeck(_deck2);
            _positionTimer.Tick -= PositionTimerOnTick;
            _positionTimer.Stop();
            if (_musicWatcher != null)
            {
                try
                {
                    _musicWatcher.EnableRaisingEvents = false;
                    _musicWatcher.Created -= OnMusicFolderChanged;
                    _musicWatcher.Changed -= OnMusicFolderChanged;
                    _musicWatcher.Deleted -= OnMusicFolderChanged;
                    _musicWatcher.Renamed -= OnMusicFolderRenamed;
                    _musicWatcher.Dispose();
                }
                catch { }
                finally { _musicWatcher = null; }
            }
            _playlistRefreshTimer.Tick -= PlaylistRefreshTimerOnTick;
            _playlistRefreshTimer.Stop();
            _volumeSaveTimer.Tick -= VolumeSaveTimerOnTick;
            _volumeSaveTimer.Stop();
            Closed -= OnWindowClosed;
        }

        private void ScheduleVolumeSave(string key, double volume)
        {
            _pendingVolumeSaves[key] = volume;
            if (!_volumeSaveTimer.IsEnabled)
            {
                _volumeSaveTimer.Start();
            }
        }

        private void VolumeSaveTimerOnTick(object? sender, EventArgs e)
        {
            FlushPendingVolumeSaves();
        }

        private void FlushPendingVolumeSaves()
        {
            _volumeSaveTimer.Stop();
            if (_pendingVolumeSaves.Count == 0)
            {
                return;
            }

            foreach (var pair in _pendingVolumeSaves)
            {
                WindowSettingsManager.SetDouble(WindowKey, pair.Key, pair.Value);
            }
            _pendingVolumeSaves.Clear();
        }

        private void SaveLeftSplitterPosition()
        {
            try
            {
                var cols = MainColumnsGrid.ColumnDefinitions;
                if (cols.Count >= 3)
                {
                    var left = cols[0];
                    WindowSettingsManager.SetDouble(WindowKey, "LeftColumnWidth", left.ActualWidth);
                    var total = Math.Max(1.0, MainColumnsGrid.ActualWidth - cols[1].ActualWidth);
                    var ratio = Math.Max(0.0, Math.Min(1.0, left.ActualWidth / total));
                    WindowSettingsManager.SetDouble(WindowKey, "LeftColumnRatio", ratio);
                }
            }
            catch { }
        }

        private void RefreshPlaylist_OnClick(object sender, RoutedEventArgs e)
        {
            LoadPlaylist();
            ApplyFilter();
        }

        private void OpenMusicFolder_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = ResolveCurrentLibraryDirectory();
                folder = EnsureDirectoryExists(folder);
                _musicDirectory = folder;
                SetupMusicWatcherIfNeeded(folder);

                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Не удалось открыть папку с музыкой: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPlaylist()
        {
            _playlist.Clear();

            var directory = ResolveCurrentLibraryDirectory();
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            _musicDirectory = directory;
            SetupMusicWatcherIfNeeded(_musicDirectory);

            try
            {
                var files = Directory.EnumerateFiles(_musicDirectory, "*", SearchOption.AllDirectories)
                    .Where(f => SupportedExtensions.Contains(Path.GetExtension(f) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true));

                foreach (var file in files)
                {
                    var key = BuildMetaKey(file);
                    var meta = _meta.Tracks.TryGetValue(key, out var m) ? m : null;
                    var defaultName = GetDefaultDisplayName(file);
                    var displayName = !string.IsNullOrWhiteSpace(meta?.CustomTitle) ? meta!.CustomTitle! : defaultName;
                    var tags = meta?.Tags?.ToList() ?? new List<string>();
                    var cover = TryLoadCover(file);
                    var item = new PlaylistItem(displayName, file, meta?.Description, tags, cover);
                    _playlist.Add(item);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"LoadPlaylist failed: {ex.Message}");
            }
        }

        private string ResolveCurrentLibraryDirectory()
        {
            return _currentPlaylistLibrary switch
            {
                PlaylistLibraryKind.Background => EnsureBackgroundDirectoryExists(),
                _ => ResolveDownloadedDirectory()
            };
        }

        private string ResolveDownloadedDirectory()
        {
            return EnsureDirectoryExists(DefaultMusicDirectory);
        }

        private string EnsureBackgroundDirectoryExists() => EnsureDirectoryExists(BackgroundMusicDirectory);

        private void LoadYouTubeTracks()
        {
            _youTubeTracks.Clear();
            LogDebug("LoadYouTubeTracks clearing current collection");

            try
            {
                var entries = YouTubeTrackStorage.Load();
                LogDebug($"LoadYouTubeTracks loaded {entries.Count} entries from storage");
                foreach (var entry in entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    var track = CreateYouTubeTrackFromStorage(entry);
                    _youTubeTracks.Add(track);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"LoadYouTubeTracks failed: {ex.Message}");
            }
        }

        private YouTubeTrack CreateYouTubeTrackFromStorage(YouTubeTrackStorage entry)
        {
            LogDebug($"CreateYouTubeTrackFromStorage: {entry.VideoId} title='{entry.Title}' stream='{entry.StreamUrl}' preview='{entry.PreviewPath}' buffer='{entry.BufferPath}'");
            var displayName = string.IsNullOrWhiteSpace(entry.Title) ? "YouTube трек" : entry.Title!;
            var videoId = entry.VideoId ?? string.Empty;
            var track = new YouTubeTrack(
                displayName,
                videoId,
                entry.Description,
                entry.StreamUrl,
                entry.DurationSeconds,
                entry.PreviewPath,
                entry.BufferPath
            );

            if (string.IsNullOrWhiteSpace(entry.PreviewPath) && !string.IsNullOrWhiteSpace(videoId))
            {
                var sourceLink = ResolveYouTubeLink(track.StreamUrl, videoId) ?? $"https://www.youtube.com/watch?v={videoId}";
                ScheduleYouTubePreviewDownload(videoId, sourceLink);
            }

            return track;
        }

        private YouTubeTrack? UpsertYouTubeTrackInCollection(YouTubeTrackStorage entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.VideoId))
            {
                LogDebug("UpsertYouTubeTrackInCollection skipped due to missing entry/videoId");
                return null;
            }

            var track = CreateYouTubeTrackFromStorage(entry);
            var existingIndex = -1;
            for (var i = 0; i < _youTubeTracks.Count; i++)
            {
                if (string.Equals(_youTubeTracks[i].VideoId, entry.VideoId, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                _youTubeTracks[existingIndex] = track;
                LogDebug($"UpsertYouTubeTrackInCollection replaced track at index {existingIndex} ({entry.VideoId})");
            }
            else
            {
                _youTubeTracks.Insert(0, track);
                LogDebug($"UpsertYouTubeTrackInCollection inserted new track at top ({entry.VideoId})");
            }

            return track;
        }

        private static ImageSource? TryLoadCover(string filePath)
        {
            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                var pictures = tagFile?.Tag?.Pictures;
                if (pictures == null || pictures.Length == 0)
                {
                    return null;
                }

                var data = pictures[0].Data?.Data;
                if (data == null || data.Length == 0)
                {
                    return null;
                }

                using var ms = new MemoryStream(data);
                ms.Position = 0;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private void FilterTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void YouTubeFilterTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            LogDebug("YouTubeFilterTextBox text changed");
            ApplyYouTubeFilter();
        }

        private void ApplyFilter()
        {
            LogDebug("ApplyFilter invoked");
            var text = (FilterTextBox.Text ?? string.Empty).Trim();
            var source = PlaylistListBox.ItemsSource;
            if (source is null)
            {
                return;
            }
            var view = CollectionViewSource.GetDefaultView(source);
            if (string.IsNullOrWhiteSpace(text))
            {
                LogDebug("ApplyFilter cleared filter");
                view.Filter = null;
                return;
            }

            var query = text.ToLowerInvariant();
            view.Filter = o =>
            {
                if (o is not PlaylistItem p)
                {
                    return false;
                }

                if (p.DisplayName?.ToLowerInvariant().Contains(query) == true)
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(p.Description) && p.Description!.ToLowerInvariant().Contains(query))
                {
                    return true;
                }

                if (p.Tags.Any(t => t.ToLowerInvariant().Contains(query)))
                {
                    return true;
                }

                return false;
            };
        }

        private void ApplyYouTubeFilter()
        {
            LogDebug("ApplyYouTubeFilter invoked");
            var text = (YouTubeFilterTextBox.Text ?? string.Empty).Trim();
            var source = YouTubeListBox.ItemsSource;
            if (source is null)
            {
                return;
            }

            var view = CollectionViewSource.GetDefaultView(source);
            if (string.IsNullOrWhiteSpace(text))
            {
                view.Filter = null;
                return;
            }

            var query = text.ToLowerInvariant();
            view.Filter = o =>
            {
                if (o is not YouTubeTrack track)
                {
                    return false;
                }

                if (track.DisplayName.ToLowerInvariant().Contains(query))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(track.Description) && track.Description!.ToLowerInvariant().Contains(query))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(track.VideoId) && track.VideoId.ToLowerInvariant().Contains(query))
                {
                    return true;
                }

                return false;
            };
        }

        private void PlaylistListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedItem = PlaylistListBox.SelectedItem as PlaylistItem;
            LogDebug(_selectedItem == null
                ? "Playlist selection cleared"
                : $"Playlist selection changed: {_selectedItem.DisplayName}");
        }

        private void YouTubeListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedYouTubeTrack = YouTubeListBox.SelectedItem as YouTubeTrack;
            YouTubeEditButton.IsEnabled = _selectedYouTubeTrack != null;
            LogDebug(_selectedYouTubeTrack == null
                ? "YouTubeListBox selection cleared"
                : $"YouTubeListBox selected {_selectedYouTubeTrack.VideoId}");
        }

        private void YouTubeListBox_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (YouTubeListBox.SelectedItem is not YouTubeTrack track)
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _youTubeDragStart = e.GetPosition(YouTubeListBox);
                LogDebug($"YouTube drag start recorded at {_youTubeDragStart}");
                return;
            }

            var current = e.GetPosition(YouTubeListBox);
            if (Math.Abs(current.X - _youTubeDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _youTubeDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            LogDebug($"Starting drag-drop for YouTube track {track.VideoId}");
            DragDrop.DoDragDrop(YouTubeListBox, new DataObject(typeof(YouTubeTrack), track), DragDropEffects.Copy);
        }

        private void YouTubeItem_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
            {
                LogDebug("YouTubeItem_OnMouseLeftButtonUp ignored: event already handled");
                return;
            }

            if (sender is not ListBoxItem lbi || lbi.DataContext is not YouTubeTrack track)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source)
            {
                if (FindAncestor<MenuItem>(source) != null)
                {
                    return;
                }
            }

            PlayYouTubeTrackOnDeck1(track);
        }

        private void RefreshYouTube_OnClick(object sender, RoutedEventArgs e)
        {
            LoadYouTubeTracks();
            ApplyYouTubeFilter();
            NavigateYouTubeWebViewHome(forceReload: true);
        }

        private async void YouTubeAddButton_OnClick(object sender, RoutedEventArgs e)
        {
            LogDebug("YouTubeAddButton_OnClick invoked");
            var dialog = new YouTubeTrackDialog(allowLinkEdit: true)
            {
                Owner = this
            };
            dialog.SetInitialValues(null, null, null);

            if (dialog.ShowDialog() != true)
            {
                LogDebug("YouTubeAddButton dialog cancelled");
                return;
            }

            var link = dialog.SelectedLink;
            if (string.IsNullOrWhiteSpace(link))
            {
                return;
            }

            try
            {
                if (!await EnsureExternalToolsAvailableAsync())
                {
                    MessageBox.Show(this,
                        "Не удалось подготовить инструменты для работы с YouTube. Повторите позже.",
                        "YouTube трек",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var metadata = await Task.Run(() => FetchYouTubeMetadata(link));
                if (metadata == null)
                {
                    LogDebug("FetchYouTubeMetadata returned null; falling back to manual entry.");
                    metadata = CreateManualYouTubeEntry(link, dialog.TitleInput, dialog.DescriptionInput);

                    if (metadata == null)
                    {
                        MessageBox.Show(this,
                            "Не удалось получить данные трека. Убедитесь, что yt-dlp установлен и ссылка корректна.",
                            "YouTube трек",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(dialog.TitleInput))
                    {
                        metadata.Title = dialog.TitleInput;
                    }

                    metadata.Description = dialog.DescriptionInput ?? metadata.Description;
                }

                if (string.IsNullOrWhiteSpace(metadata.VideoId))
                {
                    MessageBox.Show(this,
                        "Не удалось определить идентификатор видео.",
                        "YouTube трек",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                UpsertYouTubeEntry(metadata);
                LogDebug($"YouTubeAddButton storing metadata for {metadata.VideoId} ({metadata.Title})");
                var insertedTrack = UpsertYouTubeTrackInCollection(metadata);
                ApplyYouTubeFilter();

                if (insertedTrack != null)
                {
                    LogDebug($"YouTubeAddButton inserted track {insertedTrack.VideoId} into collection");
                    YouTubeListBox.SelectedItem = insertedTrack;
                    YouTubeListBox.ScrollIntoView(insertedTrack);
                    _selectedYouTubeTrack = insertedTrack;
                    YouTubeEditButton.IsEnabled = true;
                }
                else
                {
                    LogDebug("YouTubeAddButton fallback refresh due to missing inserted track");
                    RefreshYouTubeList(metadata.VideoId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Не удалось обработать YouTube трек: {ex.Message}",
                    "YouTube трек",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void YouTubeEditButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_selectedYouTubeTrack == null)
            {
                return;
            }

            EditYouTubeTrack(_selectedYouTubeTrack);
        }

        private void YouTubeEditMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menu || menu.DataContext is not YouTubeTrack track)
            {
                return;
            }

            YouTubeListBox.SelectedItem = track;
            EditYouTubeTrack(track);
        }

        private void EditYouTubeTrack(YouTubeTrack track)
        {
            var entries = LoadYouTubeEntries();
            var entry = entries.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.VideoId) &&
                                                    string.Equals(x.VideoId, track.VideoId, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                MessageBox.Show(this,
                    "Не удалось найти данные выбранного трека.",
                    "YouTube трек",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var dialog = new YouTubeTrackDialog(allowLinkEdit: false)
            {
                Owner = this
            };
            dialog.SetInitialValues(entry.StreamUrl ?? track.StreamUrl, entry.Title, entry.Description);

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            entry.Title = dialog.TitleInput;
            entry.Description = dialog.DescriptionInput;

            SaveYouTubeEntries(entries);
            RefreshYouTubeList(entry.VideoId);
        }

        private void YouTubeDeleteMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menu || menu.DataContext is not YouTubeTrack track)
            {
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(track.DisplayName)
                ? "YouTube трек"
                : track.DisplayName;

            var confirm = MessageBox.Show(this,
                $"Удалить трек \"{displayName}\"?",
                "Удаление трека",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var entries = LoadYouTubeEntries();
            var removed = entries.RemoveAll(x => !string.IsNullOrWhiteSpace(x.VideoId) &&
                                                 string.Equals(x.VideoId, track.VideoId, StringComparison.OrdinalIgnoreCase));

            if (removed == 0)
            {
                MessageBox.Show(this,
                    "Не удалось найти трек в списке сохранённых.",
                    "Удаление трека",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SaveYouTubeEntries(entries);

            if (_selectedYouTubeTrack != null &&
                string.Equals(_selectedYouTubeTrack.VideoId, track.VideoId, StringComparison.OrdinalIgnoreCase))
            {
                _selectedYouTubeTrack = null;
                YouTubeListBox.SelectedItem = null;
                YouTubeEditButton.IsEnabled = false;
            }

            RefreshYouTubeList(null);
        }

        private async void YouTubeDownloadAllButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_downloadAllYouTubeInProgress)
            {
                MessageBox.Show(this, "Загрузка уже выполняется.", "Скачивание YouTube", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_youTubeTracks.Count == 0)
            {
                MessageBox.Show(this, "Список YouTube треков пуст.", "Скачивание YouTube", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var button = YouTubeDownloadAllButton;
            var originalContent = button.Content;
            button.IsEnabled = false;
            button.Content = "…";

            _downloadAllYouTubeInProgress = true;
            try
            {
                var tracks = _youTubeTracks.ToList();
                await Task.Run(() => DownloadAllYouTubeTracks(tracks));
                MessageBox.Show(this, "Все доступные треки скачаны в папку Music/YouTube.", "Скачивание завершено", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Не удалось скачать все треки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _downloadAllYouTubeInProgress = false;
                button.IsEnabled = true;
                button.Content = originalContent;
            }
        }

        private void DownloadAllYouTubeTracks(List<YouTubeTrack> tracks)
        {
            foreach (var track in tracks)
            {
                try
                {
                    if (EnsureYouTubeTrackDownloaded(track))
                    {
                        LogDebug($"YouTube track '{track.DisplayName}' скачан в кэш.");
                    }
                    else
                    {
                        LogDebug($"Не удалось скачать YouTube трек '{track.DisplayName}'.");
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"DownloadAllYouTubeTracks: исключение при обработке '{track.DisplayName}': {ex.Message}");
                }
            }
        }

        private static List<YouTubeTrackStorage> LoadYouTubeEntries()
        {
            LogDebug("LoadYouTubeEntries invoked");
            return YouTubeTrackStorage.Load();
        }

        private static void SaveYouTubeEntries(List<YouTubeTrackStorage> entries)
        {
            LogDebug($"SaveYouTubeEntries invoked: {entries.Count} entries");
            YouTubeTrackStorage.Write(entries);
        }

        private static void UpsertYouTubeEntry(YouTubeTrackStorage metadata)
        {
            LogDebug($"UpsertYouTubeEntry invoked: {metadata.VideoId}");
            var entries = LoadYouTubeEntries();
            var videoId = metadata.VideoId ?? string.Empty;
            var existingIndex = entries.FindIndex(e => !string.IsNullOrWhiteSpace(e.VideoId) &&
                                                       string.Equals(e.VideoId, videoId, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                entries[existingIndex] = metadata;
            }
            else
            {
                entries.Add(metadata);
            }

            SaveYouTubeEntries(entries);
            LogDebug($"UpsertYouTubeEntry: persisted changes for {metadata.VideoId}");
        }

        private static void UpdateYouTubeTrackStoragePreviewPath(string? videoId, string previewPath)
        {
            LogDebug($"UpdateYouTubeTrackStoragePreviewPath invoked: {videoId} -> {previewPath}");
            if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(previewPath))
            {
                LogDebug("UpdateYouTubeTrackStoragePreviewPath: invalid input");
                return;
            }

            try
            {
                var entries = LoadYouTubeEntries();
                var entry = entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.VideoId) &&
                                                        string.Equals(e.VideoId, videoId, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    LogDebug($"UpdateYouTubeTrackStoragePreviewPath: no entry found for {videoId}");
                    return;
                }

                entry.PreviewPath = previewPath;
                SaveYouTubeEntries(entries);
                LogDebug($"UpdateYouTubeTrackStoragePreviewPath: saved preview for {videoId}");
            }
            catch (Exception ex)
            {
                LogDebug($"UpdateYouTubeTrackStoragePreviewPath failed: {ex.Message}");
            }
        }

        private static void UpdateYouTubeTrackStorageStreamUrl(string? videoId, string? streamUrl)
        {
            LogDebug($"UpdateYouTubeTrackStorageStreamUrl invoked: {videoId} -> {streamUrl}");
            if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(streamUrl))
            {
                LogDebug("UpdateYouTubeTrackStorageStreamUrl: invalid input");
                return;
            }

            try
            {
                var entries = LoadYouTubeEntries();
                var entry = entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.VideoId) &&
                                                        string.Equals(e.VideoId, videoId, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    LogDebug($"UpdateYouTubeTrackStorageStreamUrl: no entry found for {videoId}");
                    return;
                }

                if (string.Equals(entry.StreamUrl, streamUrl, StringComparison.OrdinalIgnoreCase))
                {
                    LogDebug("UpdateYouTubeTrackStorageStreamUrl: value unchanged");
                    return;
                }

                entry.StreamUrl = streamUrl;
                SaveYouTubeEntries(entries);
                LogDebug($"UpdateYouTubeTrackStorageStreamUrl: saved stream url for {videoId}");
            }
            catch (Exception ex)
            {
                LogDebug($"UpdateYouTubeTrackStorageStreamUrl failed: {ex.Message}");
            }
        }

        private static void UpdateYouTubeTrackStorageBufferPath(string? videoId, string? bufferPath)
        {
            LogDebug($"UpdateYouTubeTrackStorageBufferPath invoked: {videoId} -> {bufferPath}");
            if (string.IsNullOrWhiteSpace(videoId))
            {
                LogDebug("UpdateYouTubeTrackStorageBufferPath: invalid videoId");
                return;
            }

            try
            {
                var entries = LoadYouTubeEntries();
                var entry = entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.VideoId) &&
                                                        string.Equals(e.VideoId, videoId, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    LogDebug($"UpdateYouTubeTrackStorageBufferPath: no entry found for {videoId}");
                    return;
                }

                entry.BufferPath = bufferPath;
                SaveYouTubeEntries(entries);
                LogDebug($"UpdateYouTubeTrackStorageBufferPath: stored buffer for {videoId}");
            }
            catch (Exception ex)
            {
                LogDebug($"UpdateYouTubeTrackStorageBufferPath failed: {ex.Message}");
            }
        }

        private static YouTubeTrackStorage? CreateManualYouTubeEntry(string link, string? title, string? description)
        {
            LogDebug("CreateManualYouTubeEntry invoked");
            if (string.IsNullOrWhiteSpace(link))
            {
                return null;
            }

            var videoId = ExtractYouTubeVideoId(link);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                LogDebug("CreateManualYouTubeEntry: could not parse video id");
                return null;
            }

            var finalTitle = string.IsNullOrWhiteSpace(title) ? "YouTube трек" : title.Trim();
            return new YouTubeTrackStorage
            {
                Title = finalTitle,
                VideoId = videoId,
                Description = description,
                StreamUrl = link,
                DurationSeconds = null
            };
        }

        private void RefreshYouTubeList(string? selectVideoId)
        {
            LogDebug($"RefreshYouTubeList invoked (select={selectVideoId ?? "<null>"})");
            LoadYouTubeTracks();
            ApplyYouTubeFilter();

            if (string.IsNullOrWhiteSpace(selectVideoId))
            {
                YouTubeListBox.SelectedItem = null;
                LogDebug("RefreshYouTubeList cleared selection");
                return;
            }

            var match = _youTubeTracks.FirstOrDefault(t => string.Equals(t.VideoId, selectVideoId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                YouTubeListBox.SelectedItem = match;
                YouTubeListBox.ScrollIntoView(match);
                LogDebug($"RefreshYouTubeList selected {selectVideoId}");
            }
            else
            {
                YouTubeListBox.SelectedItem = null;
                LogDebug($"RefreshYouTubeList: track {selectVideoId} not found after refresh");
            }
        }

        private void PlayYouTubeTrackOnDeck1(YouTubeTrack track, double? startSeconds = null, bool autoPlay = true) =>
            PlayYouTubeTrackOnDeck(_deck1, track, startSeconds, autoPlay);

        private void PlayYouTubeTrackOnDeck2(YouTubeTrack track, double? startSeconds = null, bool autoPlay = true) =>
            PlayYouTubeTrackOnDeck(_deck2, track, startSeconds, autoPlay);

        private void PlayYouTubeTrackOnDeck3(YouTubeTrack track, double? startSeconds = null, bool autoPlay = true) =>
            PlayYouTubeTrackOnDeck(_deck3, track, startSeconds, autoPlay);

        private void PlayYouTubeTrackOnDeck4(YouTubeTrack track, double? startSeconds = null, bool autoPlay = true) =>
            PlayYouTubeTrackOnDeck(_deck4, track, startSeconds, autoPlay);

        private PlaylistItem CreatePlaylistItemFromYouTube(YouTubeTrack track, bool allowPreviewFallback = true)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            if (!TryEnsureYouTubeLocalFile(track, out var playbackPath, allowPreviewFallback))
            {
                throw new InvalidOperationException("Не удалось подготовить поток для воспроизведения.");
            }

            var tags = new List<string> { "YouTube" };
            var isStream = Uri.TryCreate(playbackPath, UriKind.Absolute, out var uri) &&
                           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            var headers = track.StreamHeaders ?? new List<KeyValuePair<string, string>>();
            LogDebug($"CreatePlaylistItemFromYouTube: path={playbackPath}, isStream={isStream}, allowPreviewFallback={allowPreviewFallback}");
            return new PlaylistItem(track.DisplayName, playbackPath, track.Description, tags, coverImage: null)
            {
                IsYouTubeStream = isStream,
                VideoId = track.VideoId,
                SourceYouTubeTrack = track,
                StreamHeaders = isStream ? new List<KeyValuePair<string, string>>(headers) : new List<KeyValuePair<string, string>>(),
                IsYouTubePreview = allowPreviewFallback && !string.IsNullOrWhiteSpace(track.PreviewPath) &&
                                   string.Equals(playbackPath, track.PreviewPath, StringComparison.OrdinalIgnoreCase)
            };
        }

        private bool TryEnsureYouTubeLocalFile(YouTubeTrack track, out string filePath, bool allowPreviewFallback = true)
        {
            filePath = string.Empty;
            LogDebug($"TryEnsureYouTubeLocalFile start: videoId={track.VideoId}, allowPreviewFallback={allowPreviewFallback}");

            if (TryGetLocalPathFromStreamUrl(track.StreamUrl, out var localPath))
            {
                var isPreview = !string.IsNullOrWhiteSpace(track.PreviewPath) &&
                                 string.Equals(localPath, track.PreviewPath, StringComparison.OrdinalIgnoreCase);

                if (isPreview && !allowPreviewFallback)
                {
                    LogDebug("TryEnsureYouTubeLocalFile skipping preview local path due to fallback disabled");
                    // skip preview local file when fallback not allowed
                }
                else if (!string.IsNullOrWhiteSpace(localPath))
                {
                    track.UpdateStreamUrl(localPath);
                    track.UpdateStreamHeaders(new List<KeyValuePair<string, string>>());
                    UpdateYouTubeTrackStorageStreamUrl(track.VideoId, localPath);
                    filePath = localPath;
                    LogDebug($"TryEnsureYouTubeLocalFile returning local path {localPath}");
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(track.BufferPath) && IOFile.Exists(track.BufferPath))
            {
                track.UpdateStreamUrl(track.BufferPath);
                track.UpdateStreamHeaders(new List<KeyValuePair<string, string>>());
                filePath = track.BufferPath;
                LogDebug($"TryEnsureYouTubeLocalFile using buffer path {track.BufferPath}");
                return true;
            }

            var cached = FindCachedYouTubeFile(track.VideoId);
            if (!string.IsNullOrWhiteSpace(cached) && IOFile.Exists(cached))
            {
                var cachedIsPreview = !string.IsNullOrWhiteSpace(track.PreviewPath) &&
                                      string.Equals(cached, track.PreviewPath, StringComparison.OrdinalIgnoreCase);
                if (cachedIsPreview && !allowPreviewFallback)
                {
                    LogDebug("TryEnsureYouTubeLocalFile skipping cached preview due to fallback disabled");
                    // skip preview cache when not allowed
                }
                else
                {
                    track.UpdateStreamUrl(cached);
                    track.UpdateStreamHeaders(new List<KeyValuePair<string, string>>());
                    UpdateYouTubeTrackStorageStreamUrl(track.VideoId, cached);
                    filePath = cached;
                    LogDebug($"TryEnsureYouTubeLocalFile using cached file {cached}");
                    return true;
                }
            }

            if (TryResolveStreamingUrl(track, out var streamLink, out var headers))
            {
                filePath = streamLink;
                track.UpdateStreamUrl(streamLink);
                track.UpdateStreamHeaders(headers);
                LogDebug("TryEnsureYouTubeLocalFile resolved streaming URL");
                return true;
            }

            if (allowPreviewFallback && !string.IsNullOrWhiteSpace(track.PreviewPath) && IOFile.Exists(track.PreviewPath))
            {
                filePath = track.PreviewPath!;
                track.UpdateStreamUrl(track.PreviewPath);
                track.UpdateStreamHeaders(new List<KeyValuePair<string, string>>());
                LogDebug($"TryEnsureYouTubeLocalFile falling back to preview {track.PreviewPath}");
                return true;
            }

            LogDebug($"TryEnsureYouTubeLocalFile: не удалось получить файл для трека {track.DisplayName} ({track.VideoId}).");
            return false;
        }

        private bool EnsureYouTubeTrackDownloaded(YouTubeTrack track)
        {
            var previewReady = !string.IsNullOrWhiteSpace(EnsureYouTubePreview(track));
            if (!previewReady)
            {
                return false;
            }

            var buffer = EnsureYouTubeBuffer(track);
            return !string.IsNullOrWhiteSpace(buffer);
        }

        private string? DownloadTrackAudio(YouTubeTrack track, string link)
        {
            track.SetDownloading(true);
            track.SetDownloadProgress(0);
            var lastProgressReport = DateTime.MinValue;
            try
            {
                var localFile = TryDownloadCompatibleAudio(link, track.VideoId, progress =>
                {
                    var now = DateTime.UtcNow;
                    if (lastProgressReport == DateTime.MinValue ||
                        (now - lastProgressReport).TotalSeconds >= 1 ||
                        progress >= 100d)
                    {
                        track.SetDownloadProgress(progress);
                        lastProgressReport = now;
                    }
                });
                if (!string.IsNullOrWhiteSpace(localFile))
                {
                    track.SetDownloadProgress(100);
                }

                return localFile;
            }
            finally
            {
                track.SetDownloading(false);
            }
        }

        private bool TryResolveStreamingUrl(YouTubeTrack track, out string streamLink, out List<KeyValuePair<string, string>> headers)
        {
            streamLink = string.Empty;
            headers = new List<KeyValuePair<string, string>>();

            var link = ResolveYouTubeLink(track.StreamUrl, track.VideoId);
            if (string.IsNullOrWhiteSpace(link))
            {
                LogDebug($"TryResolveStreamingUrl: отсутствует ссылка для трека {track.DisplayName} ({track.VideoId}).");
                return false;
            }

            try
            {
                var command = BuildYtDlpMetadataCommand(link);
                if (!TryRunYtDlp(command, out var output, out _))
                {
                    LogDebug("TryResolveStreamingUrl: yt-dlp metadata command failed.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    LogDebug("TryResolveStreamingUrl: yt-dlp returned empty output.");
                    return false;
                }

                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;

                string? streamUrl = TryExtractStreamUrl(root);
                string? streamExtension = ExtractExtensionFromUrl(streamUrl);
                string? streamCodec = ExtractAudioCodec(root, streamUrl);
                streamUrl = SanitizeStreamUrl(streamUrl, streamExtension, streamCodec, root);

                if (string.IsNullOrWhiteSpace(streamUrl))
                {
                    LogDebug("TryResolveStreamingUrl: не удалось выбрать подходящий поток.");
                    return false;
                }

                streamLink = streamUrl;
                headers = BuildYouTubeStreamHeaders(link);
                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"TryResolveStreamingUrl exception: {ex.Message}");
                return false;
            }
        }

        private static bool TryGetLocalPathFromStreamUrl(string? streamUrl, out string localPath)
        {
            localPath = string.Empty;

            if (string.IsNullOrWhiteSpace(streamUrl))
            {
                return false;
            }

            if (IOFile.Exists(streamUrl))
            {
                localPath = streamUrl;
                return true;
            }

            if (Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                localPath = uri.LocalPath;
                if (IOFile.Exists(localPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? ResolveYouTubeLink(string? streamUrl, string? videoId)
        {
            if (!string.IsNullOrWhiteSpace(streamUrl) &&
                Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var host = uri.Host;
                var isYouTubeHost = host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                                     host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase);
                var isGoogleVideoHost = host.EndsWith("googlevideo.com", StringComparison.OrdinalIgnoreCase);

                if (isYouTubeHost || string.IsNullOrWhiteSpace(videoId) || !isGoogleVideoHost)
                {
                    return streamUrl;
                }
            }

            if (!string.IsNullOrWhiteSpace(videoId))
            {
                return $"https://www.youtube.com/watch?v={videoId}";
            }

            return null;
        }

        private static string? FindCachedYouTubeFile(string? videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            try
            {
                if (!Directory.Exists(YouTubeCacheDirectory))
                {
                    return null;
                }

                var match = Directory.EnumerateFiles(YouTubeCacheDirectory)
                    .Select(path => new
                    {
                        path,
                        fileName = Path.GetFileName(path) ?? string.Empty,
                        ext = NormalizeExtension(Path.GetExtension(path))
                    })
                    .Where(entry => entry.fileName.StartsWith(videoId, StringComparison.OrdinalIgnoreCase) && IsPlayable(entry.ext, null))
                    .Select(entry => new
                    {
                        entry.path,
                        priority = GetCachePriority(Path.GetFileNameWithoutExtension(entry.path))
                    })
                    .OrderBy(entry => entry.priority)
                    .ThenByDescending(entry => IOFile.GetLastWriteTimeUtc(entry.path))
                    .FirstOrDefault();

                return match?.path;
            }
            catch (Exception ex)
            {
                LogDebug($"FindCachedYouTubeFile exception: {ex.Message}");
                return null;
            }
        }

        private static int GetCachePriority(string? fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return 2;
            }

            if (fileNameWithoutExtension.EndsWith(YouTubeBufferSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (fileNameWithoutExtension.EndsWith(YouTubePreviewSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 2;
        }

        private void TrackSourcesTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
            {
                return;
            }

            var selectedTab = TrackSourcesTabControl.SelectedItem;

            if (Equals(selectedTab, YouTubeTabItem))
            {
                ApplyYouTubeFilter();
            }
            else
            {
                YouTubeEditButton.IsEnabled = false;
                _selectedYouTubeTrack = null;
                if (YouTubeListBox != null)
                {
                    YouTubeListBox.SelectedItem = null;
                }

                var library = Equals(selectedTab, BackgroundTabItem)
                    ? PlaylistLibraryKind.Background
                    : PlaylistLibraryKind.Downloaded;
                SwitchToPlaylistLibrary(library);
            }

            UpdateDeckColumnVisibility(IsLocalPlaylistTab(selectedTab));
        }

        private void SwitchToPlaylistLibrary(PlaylistLibraryKind library)
        {
            var targetTab = library == PlaylistLibraryKind.Background ? BackgroundTabItem : DownloadedTabItem;
            EnsurePlaylistTabContentHost(targetTab);

            var changed = _currentPlaylistLibrary != library;
            _currentPlaylistLibrary = library;

            if (changed)
            {
                LoadPlaylist();
            }

            ApplyFilter();
        }

        private void EnsurePlaylistTabContentHost(TabItem? target)
        {
            if (target == null || PlaylistTabContent == null || ReferenceEquals(_playlistTabHost, target))
            {
                return;
            }

            if (_playlistTabHost != null)
            {
                _playlistTabHost.Content = null;
            }

            target.Content = PlaylistTabContent;
            _playlistTabHost = target;
        }

        private static string EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"EnsureDirectoryExists failed for '{path}': {ex.Message}");
            }

            return path;
        }

        private bool IsLocalPlaylistTab(object? tab)
        {
            return Equals(tab, DownloadedTabItem) || Equals(tab, BackgroundTabItem);
        }

        private void PlaylistItem_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (sender is not ListBoxItem lbi || lbi.DataContext is not PlaylistItem item)
            {
                return;
            }

            if (e.OriginalSource is DependencyObject source)
            {
                if (FindAncestor<WpfTextBox>(source) != null)
                {
                    return;
                }
                if (FindAncestor<Hyperlink>(source) != null)
                {
                    return;
                }
            }

            PlayPlaylistItemOnDeck1(item);
        }

        private void SaveMeta_OnClick(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null)
            {
                return;
            }

            var key = BuildMetaKey(_selectedItem.FilePath);
            var meta = MusicDataManager.GetOrCreate(_meta, key);
            meta.Description = _selectedItem.Description ?? string.Empty;
            meta.Tags = _selectedItem.Tags.ToList();
            MusicDataManager.SaveAll(_meta);

            RefreshPlaylistView();
        }

        private void ClearMeta_OnClick(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null)
            {
                return;
            }

            _selectedItem.Description = string.Empty;
            _selectedItem.Tags = new List<string>();
            _selectedItem = ResetPlaylistItemTitle(_selectedItem);
            PersistPlaylistMetadata(_selectedItem);
            RefreshPlaylistView();
        }

        private static List<string> ParseTags(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void RefreshPlaylistView()
        {
            var view = CollectionViewSource.GetDefaultView(PlaylistListBox.ItemsSource);
            view.Refresh();
        }

        private void DescriptionTextBlock_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is PlaylistItem item)
            {
                RenderDescriptionTextBlock(tb, item.Description);
            }
        }

        private void DescriptionTextBlock_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                var item = tb.DataContext as PlaylistItem;
                RenderDescriptionTextBlock(tb, item?.Description);
            }
        }

        private void DescriptionTextBlock_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBlock tb)
            {
                return;
            }

            if (e.OriginalSource is Run run && run.Parent is Hyperlink)
            {
                return;
            }

            if (tb.DataContext is PlaylistItem item)
            {
                EditPlaylistItem(item);
                e.Handled = true;
            }
        }

        private void PlaylistEditMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menu || menu.DataContext is not PlaylistItem item)
            {
                return;
            }

            PlaylistListBox.SelectedItem = item;
            EditPlaylistItem(item);
        }

        private void EditPlaylistItem(PlaylistItem item)
        {
            var dialog = new YouTubeTrackDialog(allowLinkEdit: false)
            {
                Owner = this,
                Title = "Метаданные трека"
            };
            dialog.SetInitialValues(item.FilePath, item.DisplayName, item.Description);

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var newTitle = dialog.TitleInput;
            var newDescription = dialog.DescriptionInput;

            if (!string.IsNullOrWhiteSpace(newTitle) && !string.Equals(newTitle, item.DisplayName, StringComparison.Ordinal))
            {
                UpdatePlaylistItemTitle(item, newTitle);
            }

            item.Description = newDescription ?? string.Empty;
            PersistPlaylistMetadata(item);
            RefreshPlaylistView();
        }

        private PlaylistItem UpdatePlaylistItemTitle(PlaylistItem item, string newTitle)
        {
            var key = BuildMetaKey(item.FilePath);
            var meta = MusicDataManager.GetOrCreate(_meta, key);
            meta.CustomTitle = newTitle;
            MusicDataManager.SaveAll(_meta);
            return ReplacePlaylistItem(item, newTitle);
        }

        private void PersistPlaylistMetadata(PlaylistItem item)
        {
            var key = BuildMetaKey(item.FilePath);
            var meta = MusicDataManager.GetOrCreate(_meta, key);
            meta.Description = item.Description;
            meta.Tags = item.Tags.ToList();
            MusicDataManager.SaveAll(_meta);
        }

        private PlaylistItem ResetPlaylistItemTitle(PlaylistItem item)
        {
            var key = BuildMetaKey(item.FilePath);
            var meta = MusicDataManager.GetOrCreate(_meta, key);
            meta.CustomTitle = null;
            MusicDataManager.SaveAll(_meta);
            var defaultName = GetDefaultDisplayName(item.FilePath);
            return ReplacePlaylistItem(item, defaultName);
        }

        private PlaylistItem ReplacePlaylistItem(PlaylistItem item, string displayName)
        {
            var replacement = new PlaylistItem(displayName, item.FilePath, item.Description, item.Tags, item.CoverImage)
            {
                IsYouTubeStream = item.IsYouTubeStream,
                VideoId = item.VideoId
            };
            var index = _playlist.IndexOf(item);
            if (index >= 0)
            {
                _playlist[index] = replacement;
                PlaylistListBox.SelectedItem = replacement;
            }

            return index >= 0 ? replacement : item;
        }

        private static string GetDefaultDisplayName(string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath) ?? "Трек";
        }

        private void DescriptionTimecodeHyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            if (_hyperlinkDragInProgress)
            {
                return;
            }

            if (sender is not Hyperlink hyperlink)
            {
                return;
            }

            e.Handled = true;

            if (hyperlink.Tag is not TimecodeLinkData data)
            {
                return;
            }

            if (hyperlink.DataContext is PlaylistItem item)
            {
                PlayPlaylistItemOnDeck1(item, data.Seconds);
            }
        }

        private void DescriptionTimecodeHyperlink_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Hyperlink hyperlink)
            {
                _activeTimecodeHyperlink = hyperlink;
                _hyperlinkDragStart = e.GetPosition(this);
                _hyperlinkDragInProgress = false;
            }
        }

        private void DescriptionTimecodeHyperlink_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            var hyperlink = _activeTimecodeHyperlink;
            if (hyperlink == null || !ReferenceEquals(sender, hyperlink))
            {
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed || _hyperlinkDragInProgress)
            {
                return;
            }

            var current = e.GetPosition(this);
            if (Math.Abs(current.X - _hyperlinkDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _hyperlinkDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (hyperlink.Tag is not TimecodeLinkData data)
            {
                return;
            }

            if (hyperlink.DataContext is not PlaylistItem item)
            {
                return;
            }

            _hyperlinkDragInProgress = true;

            var payload = new TimedPlaylistDrag(item, data.Seconds, data.Token);
            var dragData = new DataObject();
            dragData.SetData(typeof(TimedPlaylistDrag), payload);
            dragData.SetData("TimedPlaylistSeconds", data.Seconds);
            dragData.SetData("TimedPlaylistToken", data.Token);
            dragData.SetData(typeof(PlaylistItem), item);
            DragDrop.DoDragDrop(hyperlink, dragData, DragDropEffects.Copy);
            _activeTimecodeHyperlink = null;
            _hyperlinkDragInProgress = false;
        }

        private void DescriptionTimecodeHyperlink_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_hyperlinkDragInProgress)
            {
                e.Handled = true;
            }

            _activeTimecodeHyperlink = null;
            _hyperlinkDragInProgress = false;
        }

        private void PlaylistListBox_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && PlaylistListBox.SelectedItem is PlaylistItem item)
            {
                DragDrop.DoDragDrop(PlaylistListBox, new DataObject(typeof(PlaylistItem), item), DragDropEffects.Copy);
            }
        }

        private void DeckNowLabel_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not TextBlock label)
            {
                return;
            }

            DeckState? deck = label == Deck1NowLabel ? _deck1
                : label == Deck2NowLabel ? _deck2
                : label == Deck3NowLabel ? _deck3
                : label == Deck4NowLabel ? _deck4
                : null;

            if (deck == null)
            {
                return;
            }

            if (!ReferenceEquals(_activeDeckLabelDrag, label) || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (deck.Queue.Count == 0)
            {
                return;
            }

            var currentIndex = Math.Clamp(deck.CurrentIndex, 0, deck.Queue.Count - 1);
            var item = deck.Queue[currentIndex];
            DragDrop.DoDragDrop(label, new DataObject(typeof(PlaylistItem), item), DragDropEffects.Copy);
            _activeDeckLabelDrag = null;
        }

        private void DeckNowLabel_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock label)
            {
                _activeDeckLabelDrag = label;
            }
        }

        private void DeckNowLabel_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ReferenceEquals(_activeDeckLabelDrag, sender))
            {
                _activeDeckLabelDrag = null;
            }
        }

        private void Deck_OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PlaylistItem)) ||
                e.Data.GetDataPresent(typeof(TimedPlaylistDrag)) ||
                e.Data.GetDataPresent(typeof(YouTubeTrack)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Deck_OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(PlaylistItem)) ||
                e.Data.GetDataPresent(typeof(TimedPlaylistDrag)) ||
                e.Data.GetDataPresent(typeof(YouTubeTrack)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Deck1_OnDrop(object sender, DragEventArgs e)
        {
            HandleDeckDrop(_deck1, e);
        }

        private void Deck2_OnDrop(object sender, DragEventArgs e) => HandleDeckDrop(_deck2, e);

        private void Deck3_OnDrop(object sender, DragEventArgs e) => HandleDeckDrop(_deck3, e);

        private void Deck4_OnDrop(object sender, DragEventArgs e) => HandleDeckDrop(_deck4, e);

        private void HandleDeckDrop(DeckState deck, DragEventArgs e)
        {
            var deckName = GetDeckName(deck);
            var label = GetNowLabel(deck);
            if (e.Data.GetData(typeof(TimedPlaylistDrag)) is TimedPlaylistDrag timed)
            {
                LogDebug($"Deck drop: timed playlist item {timed.Item.DisplayName} at {timed.Seconds:F3}s");
                var sameTrackLoaded = deck.Stream != null && deck.LoadedFilePath != null &&
                    string.Equals(deck.LoadedFilePath, timed.Item.FilePath, StringComparison.OrdinalIgnoreCase);
                var wasPlaying = deck.WaveOut != null && deck.WaveOut.PlaybackState == PlaybackState.Playing;

                LogDebug($"Drop timed track on {deckName}: item={timed.Item.DisplayName}, seconds={timed.Seconds:F3}, sameLoaded={sameTrackLoaded}, wasPlaying={wasPlaying}");

                PlayPlaylistItemOnDeck(deck, timed.Item, timed.Seconds, autoPlay: true);
                return;
            }

            TimedPlaylistDrag? fallbackTimed = null;
            if (e.Data.GetData(typeof(PlaylistItem)) is PlaylistItem fallbackItem &&
                e.Data.GetDataPresent("TimedPlaylistSeconds") &&
                e.Data.GetData("TimedPlaylistSeconds") is double seconds)
            {
                var token = e.Data.GetData("TimedPlaylistToken") as string ?? string.Empty;
                fallbackTimed = new TimedPlaylistDrag(fallbackItem, seconds, token);
            }

            if (fallbackTimed != null)
            {
                var sameTrackLoaded = deck.Stream != null && deck.LoadedFilePath != null &&
                    string.Equals(deck.LoadedFilePath, fallbackTimed.Item.FilePath, StringComparison.OrdinalIgnoreCase);
                var wasPlaying = deck.WaveOut != null && deck.WaveOut.PlaybackState == PlaybackState.Playing;
                LogDebug($"Drop fallback timed track on {deckName}: item={fallbackTimed.Item.DisplayName}, seconds={fallbackTimed.Seconds:F3}, sameLoaded={sameTrackLoaded}, wasPlaying={wasPlaying}");
                PlayPlaylistItemOnDeck(deck, fallbackTimed.Item, fallbackTimed.Seconds, autoPlay: true);
                return;
            }

            if (e.Data.GetData(typeof(YouTubeTrack)) is YouTubeTrack yt)
            {
                LogDebug($"Drop YouTube track on {deckName}: item={yt.DisplayName}");
                PlayYouTubeTrackOnDeck(deck, yt, autoPlay: true);
                return;
            }

            if (e.Data.GetData(typeof(PlaylistItem)) is PlaylistItem item)
            {
                deck.PendingSeekSeconds = null;
                PlayPlaylistItemOnDeck(deck, item, autoPlay: false);
                LogDebug($"Drop plain track on {deckName}: item={item.DisplayName}");
            }
            else
            {
                LogDebug($"Drop on {deckName} with formats: {string.Join(",", e.Data.GetFormats())}");
            }
        }

        private void SeekDeck(DeckState deck, double seconds, TextBlock label)
        {
            if (deck.Queue.Count == 0)
            {
                return;
            }

            var targetSeconds = Math.Max(0, seconds);
            var slider = GetSeekSlider(deck);
            var timeLabel = GetSeekTimeLabel(deck);
            var currentTrackPath = deck.CurrentIndex >= 0 && deck.CurrentIndex < deck.Queue.Count
                ? deck.Queue[deck.CurrentIndex].FilePath
                : null;
            var hasActiveStream = deck.Stream != null &&
                currentTrackPath != null &&
                string.Equals(deck.LoadedFilePath, currentTrackPath, StringComparison.OrdinalIgnoreCase);

            LogDebug($"SeekDeck {GetDeckName(deck)} -> seconds={targetSeconds:F3}, hasStream={deck.Stream != null}, hasActiveStream={hasActiveStream}, pending={deck.PendingSeekSeconds:F3}");

            if (!hasActiveStream)
            {
                deck.PendingSeekSeconds = targetSeconds;
                var headroom = Math.Max(30.0, targetSeconds * 0.25);
                var desiredMax = Math.Max(1.0, Math.Max(slider.Maximum, targetSeconds + headroom));
                try
                {
                    _isUpdatingSliders = true;
                    slider.Minimum = 0;
                    slider.Maximum = desiredMax;
                    slider.Value = Math.Min(desiredMax, targetSeconds);
                }
                finally
                {
                    _isUpdatingSliders = false;
                }
                UpdateSeekTimeLabel(targetSeconds, timeLabel);
                UpdateDeckNowLabel(deck, label);
                LogDebug($"SeekDeck pending (no active stream). SliderMax={slider.Maximum:F3}, value={slider.Value:F3}");
                return;
            }

            var stream = deck.Stream;
            if (stream == null)
            {
                deck.PendingSeekSeconds = targetSeconds;
                LogDebug("SeekDeck stream unexpectedly null despite active flag; storing pending seek");
                return;
            }

            var total = GetTotalSeconds(stream);
            if (total > 0)
            {
                targetSeconds = Math.Min(targetSeconds, total);
            }

            try
            {
                if (deck.UseVlc)
                {
                    ApplyVlcSeek(deck, targetSeconds);
                    deck.PendingSeekSeconds = null;
                    UpdateSeekSliderForDeck(deck, slider);
                    var vlcWasPlaying = deck.VlcPlayer != null && deck.VlcPlayer.IsPlaying;
                    LogDebug($"SeekDeck applied via VLC -> currentTime={(deck.VlcPlayer?.Time ?? 0) / 1000.0:F3}, isPlaying={vlcWasPlaying}");
                    if (!vlcWasPlaying)
                    {
                        UpdateDeckNowLabel(deck, label);
                    }
                    return;
                }

                SetStreamPosition(stream, targetSeconds);
                deck.PendingSeekSeconds = null;
                UpdateSeekSliderForDeck(deck, slider);
                var deckWasPlaying = deck.WaveOut != null && deck.WaveOut.PlaybackState == PlaybackState.Playing;
                LogDebug($"SeekDeck applied -> currentTime={stream.CurrentTime.TotalSeconds:F3}, isPlaying={deckWasPlaying}");
                if (!deckWasPlaying)
                {
                    UpdateDeckNowLabel(deck, label);
                }
            }
            catch
            {
                // ignore seek failures
            }
        }

        private static void ApplyVlcSeek(DeckState deck, double seconds)
        {
            if (deck == null)
            {
                return;
            }

            if (deck.VlcPlayer == null)
            {
                deck.PendingSeekSeconds = seconds;
                return;
            }

            var clampedSeconds = Math.Max(0.0, seconds);
            var lengthMs = deck.VlcPlayer.Length;
            if (lengthMs > 0)
            {
                clampedSeconds = Math.Min(clampedSeconds, lengthMs / 1000.0);
            }

            deck.VlcPlayer.Time = (long)(clampedSeconds * 1000);
        }

        private static bool TryExtractTimecodeToken(string text, int index, out string token, out double seconds)
        {
            token = string.Empty;
            seconds = 0;

            foreach (Match match in TimecodeRegex.Matches(text))
            {
                if (index < match.Index || index >= match.Index + match.Length)
                {
                    continue;
                }

                if (!TryParseTimecode(match, out seconds))
                {
                    continue;
                }

                token = match.Value;
                return true;
            }

            return false;
        }

        private static bool TryParseTimecode(Match match, out double seconds)
        {
            seconds = 0;
            try
            {
                var hourGroup = match.Groups["hours"];
                var minuteGroup = match.Groups["minutes"];
                var secondGroup = match.Groups["seconds"];

                var hour = hourGroup.Success ? int.Parse(hourGroup.Value) : 0;
                var minute = int.Parse(minuteGroup.Value);
                var second = int.Parse(secondGroup.Value);

                if (second >= 60 || minute < 0 || second < 0)
                {
                    return false;
                }

                seconds = hour * 3600 + minute * 60 + second;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private record TimedPlaylistDrag(PlaylistItem Item, double Seconds, string SourceToken);

        private static void SetStreamPosition(WaveStream stream, double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            var total = GetTotalSeconds(stream);
            if (total > 0)
            {
                seconds = Math.Min(seconds, total);
            }

            try
            {
                stream.CurrentTime = TimeSpan.FromSeconds(seconds);
            }
            catch
            {
                // ignore CurrentTime seek errors
            }

            if (!stream.CanSeek)
            {
                return;
            }

            try
            {
                var format = stream.WaveFormat;
                if (format.AverageBytesPerSecond > 0)
                {
                    var targetPos = (long)(format.AverageBytesPerSecond * seconds);
                    var blockAlign = format.BlockAlign;
                    if (blockAlign > 0)
                    {
                        targetPos -= targetPos % blockAlign;
                    }
                    targetPos = Math.Max(0, Math.Min(targetPos, stream.Length));
                    stream.Position = targetPos;
                    LogDebug($"SetStreamPosition adjusted byte offset -> {targetPos} ({seconds:F3}s)");
                }

                if (stream is MediaFoundationReader mfr)
                {
                    mfr.Flush();
                    LogDebug("SetStreamPosition flushed MediaFoundationReader buffers");
                }
            }
            catch
            {
                // ignore Position seek errors
            }
        }

        private void AddToDeck(DeckState deck, PlaylistItem item, TextBlock label)
        {
            deck.Queue.Add(item);
            if (deck.CurrentIndex < 0)
            {
                deck.CurrentIndex = 0;
            }
            UpdateDeckNowLabel(deck, label);
        }

        private bool ReplaceDeck(DeckState deck, PlaylistItem item, TextBlock label, bool autoResume = true)
        {
            var wasPlaying = deck.WaveOut != null && deck.WaveOut.PlaybackState == PlaybackState.Playing;
            LogDebug($"ReplaceDeck {(ReferenceEquals(deck, _deck1) ? "Deck1" : "Deck2")} -> item={item.DisplayName}, wasPlaying={wasPlaying}, autoResume={autoResume}");

            var sameAsCurrent = deck.Queue.Count > 0 && deck.CurrentIndex >= 0 && deck.CurrentIndex < deck.Queue.Count &&
                                string.Equals(deck.Queue[deck.CurrentIndex].FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase);

            if (sameAsCurrent)
            {
                deck.Queue[deck.CurrentIndex] = item;
                UpdateDeckNowLabel(deck, label);
                return wasPlaying;
            }

            deck.Queue.Clear();
            deck.Queue.Add(item);
            deck.CurrentIndex = 0;

            if (wasPlaying && autoResume)
            {
                PlayCurrent(deck, label, deck == _deck1 ? OnDeck1PlaybackStopped : OnDeck2PlaybackStopped);
            }
            else
            {
                UpdateDeckNowLabel(deck, label);
            }

            return wasPlaying;
        }

        private void Deck1PlayPauseToggle_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleHandlers)
            {
                return;
            }

            if (!_deck1.IsPlaying)
            {
                PlayOrResume(_deck1, Deck1NowLabel, OnDeck1PlaybackStopped);
            }

            _deck1.IsPlaying = true;
            _deck1.ResumePosition = GetSliderPosition(Deck1SeekSlider);
            Deck1PlayPauseToggle.Content = "⏸";
            SaveDeckState();
        }

        private void Deck1PlayPauseToggle_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleHandlers)
            {
                return;
            }

            PauseDeck(_deck1);
            Deck1PlayPauseToggle.Content = "⏵";
            SaveDeckState();
        }

        private void Deck2PlayPauseToggle_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleHandlers)
            {
                return;
            }

            if (!_deck2.IsPlaying)
            {
                PlayOrResume(_deck2, Deck2NowLabel, OnDeck2PlaybackStopped);
            }

            _deck2.IsPlaying = true;
            _deck2.ResumePosition = GetSliderPosition(Deck2SeekSlider);
            Deck2PlayPauseToggle.Content = "⏸";
            SaveDeckState();
        }

        private void Deck2PlayPauseToggle_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleHandlers)
            {
                return;
            }

            PauseDeck(_deck2);
            Deck2PlayPauseToggle.Content = "⏵";
            SaveDeckState();
        }

        private void Deck3PlayPauseToggle_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleHandlers)
            {
                return;
            }

            if (!_deck3.IsPlaying)
            {
                PlayOrResume(_deck3, Deck3NowLabel, OnDeck3PlaybackStopped);
            }

            _deck3.IsPlaying = true;
            _deck3.ResumePosition = GetSliderPosition(Deck3SeekSlider);
            Deck3PlayPauseToggle.Content = "⏸";
            SaveDeckState();
        }

        private void Deck3PlayPauseToggle_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleHandlers)
            {
                return;
            }

            PauseDeck(_deck3);
            Deck3PlayPauseToggle.Content = "⏵";
            SaveDeckState();
        }

        private void Deck4PlayPauseToggle_OnChecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleHandlers)
            {
                return;
            }

            if (!_deck4.IsPlaying)
            {
                PlayOrResume(_deck4, Deck4NowLabel, OnDeck4PlaybackStopped);
            }

            _deck4.IsPlaying = true;
            _deck4.ResumePosition = GetSliderPosition(Deck4SeekSlider);
            Deck4PlayPauseToggle.Content = "⏸";
            SaveDeckState();
        }

        private void Deck4PlayPauseToggle_OnUnchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressToggleHandlers)
            {
                return;
            }

            PauseDeck(_deck4);
            Deck4PlayPauseToggle.Content = "⏵";
            SaveDeckState();
        }

        private void PauseDeck(DeckState deck)
        {
            if (deck.UseVlc)
            {
                deck.VlcPlayer?.Pause();
            }
            else if (deck.WaveOut != null)
            {
                deck.WaveOut.Pause();
            }

            deck.IsPlaying = false;
            deck.ResumePosition = GetSliderPosition(GetSeekSlider(deck));
        }

        private void Deck1VolumeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_deck1.IsVolumeAutomationUpdate)
            {
                CancelDeckFade(_deck1);
            }
            _deck1.Volume = Math.Clamp(e.NewValue, 0.0, 1.0);
            if (_deck1.UseVlc)
            {
                if (_deck1.VlcPlayer != null)
                {
                    _deck1.VlcPlayer.Volume = (int)(_deck1.Volume * 100);
                }
            }
            else if (_deck1.VolumeProvider != null)
            {
                _deck1.VolumeProvider.Volume = (float)_deck1.Volume;
            }
            if (!_deck1.IsVolumeAutomationUpdate)
            {
                ScheduleVolumeSave("Deck1Volume", _deck1.Volume);
            }
        }

        private void Deck2VolumeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_deck2.IsVolumeAutomationUpdate)
            {
                CancelDeckFade(_deck2);
            }
            _deck2.Volume = Math.Clamp(e.NewValue, 0.0, 1.0);
            if (_deck2.UseVlc)
            {
                if (_deck2.VlcPlayer != null)
                {
                    _deck2.VlcPlayer.Volume = (int)(_deck2.Volume * 100);
                }
            }
            else if (_deck2.VolumeProvider != null)
            {
                _deck2.VolumeProvider.Volume = (float)_deck2.Volume;
            }
            if (!_deck2.IsVolumeAutomationUpdate)
            {
                ScheduleVolumeSave("Deck2Volume", _deck2.Volume);
            }
        }

        private void Deck3VolumeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_deck3.IsVolumeAutomationUpdate)
            {
                CancelDeckFade(_deck3);
            }
            _deck3.Volume = Math.Clamp(e.NewValue, 0.0, 1.0);
            if (_deck3.UseVlc)
            {
                if (_deck3.VlcPlayer != null)
                {
                    _deck3.VlcPlayer.Volume = (int)(_deck3.Volume * 100);
                }
            }
            else if (_deck3.VolumeProvider != null)
            {
                _deck3.VolumeProvider.Volume = (float)_deck3.Volume;
            }
            if (!_deck3.IsVolumeAutomationUpdate)
            {
                ScheduleVolumeSave("Deck3Volume", _deck3.Volume);
            }
        }

        private void Deck4VolumeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_deck4.IsVolumeAutomationUpdate)
            {
                CancelDeckFade(_deck4);
            }
            _deck4.Volume = Math.Clamp(e.NewValue, 0.0, 1.0);
            if (_deck4.UseVlc)
            {
                if (_deck4.VlcPlayer != null)
                {
                    _deck4.VlcPlayer.Volume = (int)(_deck4.Volume * 100);
                }
            }
            else if (_deck4.VolumeProvider != null)
            {
                _deck4.VolumeProvider.Volume = (float)_deck4.Volume;
            }
            if (!_deck4.IsVolumeAutomationUpdate)
            {
                ScheduleVolumeSave("Deck4Volume", _deck4.Volume);
            }
        }

        private async void Deck1FadeButton_OnClick(object sender, RoutedEventArgs e)
        {
            await FadeOutDeckAsync(_deck1);
        }

        private async void Deck2FadeButton_OnClick(object sender, RoutedEventArgs e)
        {
            await FadeOutDeckAsync(_deck2);
        }

        private async void Deck3FadeButton_OnClick(object sender, RoutedEventArgs e)
        {
            await FadeOutDeckAsync(_deck3);
        }

        private async void Deck4FadeButton_OnClick(object sender, RoutedEventArgs e)
        {
            await FadeOutDeckAsync(_deck4);
        }

        private async Task FadeOutDeckAsync(DeckState deck)
        {
            if (deck == null)
            {
                return;
            }

            var slider = GetVolumeSlider(deck);
            var toggle = GetPlayToggle(deck);
            if (slider == null || toggle == null)
            {
                return;
            }

            CancelDeckFade(deck);

            var startVolume = Math.Clamp(deck.Volume, 0.0, 1.0);
            if (startVolume <= 0.0001)
            {
                PauseDeck(deck);
                SetPlayToggleState(toggle, false);
                toggle.Content = "⏵";
                return;
            }

            const double fadeDurationSeconds = 5.0;
            const int steps = 50;
            var delay = TimeSpan.FromMilliseconds(fadeDurationSeconds * 1000 / steps);

            var cts = new CancellationTokenSource();
            deck.FadeCancellation = cts;

            bool cancelled = false;

            try
            {
                for (int i = 1; i <= steps; i++)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    var progress = (double)i / steps;
                    var newVolume = Math.Max(0.0, startVolume * (1 - progress));
                    await Dispatcher.InvokeAsync(() =>
                    {
                        deck.IsVolumeAutomationUpdate = true;
                        try
                        {
                            slider.Value = newVolume;
                        }
                        finally
                        {
                            deck.IsVolumeAutomationUpdate = false;
                        }
                    }, DispatcherPriority.Background, cts.Token);

                    await Task.Delay(delay, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                deck.FadeCancellation?.Dispose();
                deck.FadeCancellation = null;
            }

            if (cancelled)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                deck.IsVolumeAutomationUpdate = true;
                try
                {
                    slider.Value = 0;
                }
                finally
                {
                    deck.IsVolumeAutomationUpdate = false;
                }

                ScheduleVolumeSave(GetVolumeSettingKey(deck), deck.Volume);
                PauseDeck(deck);
                SetPlayToggleState(toggle, false);
                toggle.Content = "⏵";
                SaveDeckState();
            }, DispatcherPriority.Normal);
        }

        private void PlayOrResume(DeckState deck, TextBlock label, EventHandler<StoppedEventArgs> onStopped)
        {
            if (deck.Queue.Count == 0)
            {
                return;
            }

            if (deck.UseVlc)
            {
                if (deck.VlcPlayer != null)
                {
                    if (deck.VlcPlayer.State == VLCState.Paused)
                    {
                        deck.VlcPlayer.Play();
                    }
                    else if (deck.VlcPlayer.State == VLCState.Stopped || deck.VlcPlayer.State == VLCState.Ended)
                    {
                        PlayCurrent(deck, label, onStopped);
                    }
                    else if (deck.VlcPlayer.State == VLCState.NothingSpecial && deck.VlcMedia != null)
                    {
                        // If VLC is in 'NothingSpecial' state but has media, it means it was stopped or not yet played.
                        // In this case, we should re-initialize and play.
                        PlayCurrent(deck, label, onStopped);
                    }
                }
                else
                {
                    // If VlcPlayer is null, it means no media is loaded or it was disposed.
                    // We should attempt to play the current item.
                    PlayCurrent(deck, label, onStopped);
                }
                return;
            }

            if (deck.WaveOut != null && deck.WaveOut.PlaybackState == PlaybackState.Paused)
            {
                deck.WaveOut.Play();
                return;
            }

            PlayCurrent(deck, label, onStopped);
        }

        private void PlayCurrent(DeckState deck, TextBlock label, EventHandler<StoppedEventArgs> onStopped)
        {
            if (deck.CurrentIndex < 0 || deck.CurrentIndex >= deck.Queue.Count)
            {
                return;
            }

            var track = deck.Queue[deck.CurrentIndex];
            var deckName = GetDeckName(deck);
            try
            {
                var pendingSeekSeconds = deck.PendingSeekSeconds;
                StopDeck(deck);
                deck.PendingSeekSeconds = pendingSeekSeconds;

                if (track.IsYouTubeStream)
                {
                    deck.UseVlc = true;
                    deck.StreamUrl = track.FilePath;
                    deck.LoadedFilePath = track.FilePath;

                    var mediaOptions = new List<string>();
                    foreach (var header in track.StreamHeaders)
                    {
                        mediaOptions.Add($":http-header={header.Key}={header.Value}");
                    }

                    deck.VlcMedia = Vlc.CreateMedia(track.FilePath, mediaOptions);
                    deck.VlcPlayer = Vlc.CreateMediaPlayer();
                    deck.VlcPlayer.Media = deck.VlcMedia;

                    deck.VlcEndReachedHandler = (sender, args) => Dispatcher.Invoke(() => onStopped(sender, new StoppedEventArgs(null)));
                    deck.VlcPlayer.Stopped += deck.VlcEndReachedHandler;
                    deck.VlcErrorHandler = (sender, args) => Dispatcher.Invoke(() =>
                    {
                        LogDebug($"VLC Error on {deckName}: playback encountered an error.");
                        onStopped(sender, new StoppedEventArgs(null)); // Treat error as stop
                    });
                    deck.VlcPlayer.EncounteredError += deck.VlcErrorHandler;

                    deck.VlcPlayer.Volume = (int)(deck.Volume * 100);

                    LogDebug($"PlayCurrent {deckName} -> track={track.DisplayName}, pending={pendingSeekSeconds?.ToString("F3") ?? "null"}, VLC stream={track.FilePath}");

                    if (deck.PendingSeekSeconds is double pendingSeek)
                    {
                        deck.VlcPlayer.Time = (long)(Math.Max(0.0, pendingSeek) * 1000);
                        deck.PendingSeekSeconds = null;
                    }

                    deck.VlcPlayer.Play();
                    LogDebug($"PlayCurrent started playback -> deck={deckName}, state={deck.VlcPlayer.State}, currentTime={deck.VlcPlayer.Time / 1000.0:F3}");
                }
                else
                {
                    deck.UseVlc = false;
                    deck.Stream = CreateWaveStream(track.FilePath);
                    deck.LoadedFilePath = track.FilePath;

                    LogDebug($"PlayCurrent {deckName} -> track={track.DisplayName}, pending={pendingSeekSeconds?.ToString("F3") ?? "null"}");

                    double initialSkip = 0;
                    if (deck.PendingSeekSeconds is double pendingSeek)
                    {
                        initialSkip = Math.Max(0.0, pendingSeek);
                        deck.PendingSeekSeconds = null;
                    }

                    var sampleProvider = deck.Stream.ToSampleProvider();

                    if (initialSkip > 0)
                    {
                        sampleProvider = new OffsetSampleProvider(sampleProvider)
                        {
                            SkipOver = TimeSpan.FromSeconds(initialSkip)
                        };
                        LogDebug($"PlayCurrent will skip via OffsetSampleProvider -> {initialSkip:F3}s");
                    }

                    deck.VolumeProvider = new VolumeSampleProvider(sampleProvider)
                    {
                        Volume = (float)Math.Clamp(deck.Volume, 0.0, 1.0)
                    };

                    deck.WaveOut = new WaveOutEvent { DeviceNumber = deck.DeviceNumber };
                    deck.WaveOut.PlaybackStopped += onStopped;
                    deck.WaveOut.Init(deck.VolumeProvider);

                    deck.WaveOut.Play();

                    LogDebug($"PlayCurrent started playback -> deck={deckName}, state={deck.WaveOut.PlaybackState}, currentTime={deck.Stream?.CurrentTime.TotalSeconds:F3}");
                }

                var slider = GetSeekSlider(deck);

                if (deck.PendingSeekSeconds is double initialSkipAfterVlcCheck && initialSkipAfterVlcCheck > 0)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateSeekSliderForDeck(deck, slider);
                    }), DispatcherPriority.Background);
                }

                label.Text = $"Играет: {track.DisplayName}";

                UpdateSeekSliderForDeck(deck, slider);
            }
            catch (Exception ex)
            {
                LogDebug($"PlayCurrent exception {deckName}: {ex.Message}");
                StopDeck(deck);
                MessageBox.Show(this, $"Не удалось воспроизвести трек \"{track.DisplayName}\": {ex.Message}", "Ошибка воспроизведения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDeck1PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_deck1.Queue.Count == 0)
            {
                StopDeck(_deck1);
                return;
            }
            _deck1.CurrentIndex = (_deck1.CurrentIndex + 1) % _deck1.Queue.Count;
            PlayCurrent(_deck1, Deck1NowLabel, OnDeck1PlaybackStopped);
        }

        private void OnDeck2PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_deck2.Queue.Count == 0)
            {
                StopDeck(_deck2);
                return;
            }
            _deck2.CurrentIndex = (_deck2.CurrentIndex + 1) % _deck2.Queue.Count;
            PlayCurrent(_deck2, Deck2NowLabel, OnDeck2PlaybackStopped);
        }

        private void OnDeck3PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_deck3.Queue.Count == 0)
            {
                StopDeck(_deck3);
                return;
            }
            _deck3.CurrentIndex = (_deck3.CurrentIndex + 1) % _deck3.Queue.Count;
            PlayCurrent(_deck3, Deck3NowLabel, OnDeck3PlaybackStopped);
        }

        private void OnDeck4PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_deck4.Queue.Count == 0)
            {
                StopDeck(_deck4);
                return;
            }
            _deck4.CurrentIndex = (_deck4.CurrentIndex + 1) % _deck4.Queue.Count;
            PlayCurrent(_deck4, Deck4NowLabel, OnDeck4PlaybackStopped);
        }

        private void DeckClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            var parameter = button.CommandParameter;

            if (parameter is not string text || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deckNumber))
            {
                deckNumber = parameter switch
                {
                    int i => i,
                    _ => 0
                };
            }

            if (deckNumber is < 1 or > 4)
            {
                return;
            }

            ClearDeck(deckNumber);
        }

        private void ClearDeck(int deckNumber)
        {
            var (deck, label, slider, timeLabel, toggle) = deckNumber switch
            {
                1 => (_deck1, Deck1NowLabel, Deck1SeekSlider, Deck1SeekTimeLabel, Deck1PlayPauseToggle),
                2 => (_deck2, Deck2NowLabel, Deck2SeekSlider, Deck2SeekTimeLabel, Deck2PlayPauseToggle),
                3 => (_deck3, Deck3NowLabel, Deck3SeekSlider, Deck3SeekTimeLabel, Deck3PlayPauseToggle),
                4 => (_deck4, Deck4NowLabel, Deck4SeekSlider, Deck4SeekTimeLabel, Deck4PlayPauseToggle),
                _ => throw new ArgumentOutOfRangeException(nameof(deckNumber), deckNumber, null)
            };

            StopDeck(deck);

            deck.Queue.Clear();
            deck.CurrentIndex = -1;
            deck.PendingSeekSeconds = null;
            deck.ResumePosition = 0;
            deck.IsPlaying = false;
            deck.LoadedFilePath = null;

            SafeUpdateSlider(slider, 0, 1, 0);
            UpdateSeekTimeLabel(0, timeLabel);
            SetPlayToggleState(toggle, false);
            toggle.Content = "⏵";

            UpdateDeckNowLabel(deck, label);
            SaveDeckState();
        }

        private void StopDeck(DeckState deck)
        {
            if (deck.WaveOut != null)
            {
                var handler = GetPlaybackStoppedHandler(deck);
                deck.WaveOut.PlaybackStopped -= handler;
                deck.WaveOut.Stop();
                deck.WaveOut.Dispose();
                deck.WaveOut = null;
            }

            deck.VolumeProvider = null;

            if (deck.VlcPlayer != null)
            {
                if (deck.VlcEndReachedHandler != null)
                {
                    deck.VlcPlayer.Stopped -= deck.VlcEndReachedHandler;
                    deck.VlcEndReachedHandler = null;
                }
                if (deck.VlcErrorHandler != null)
                {
                    deck.VlcPlayer.EncounteredError -= deck.VlcErrorHandler;
                    deck.VlcErrorHandler = null;
                }
                deck.VlcPlayer.Stop();
                deck.VlcPlayer.Dispose();
                deck.VlcPlayer = null;
            }

            if (deck.VlcMedia != null)
            {
                deck.VlcMedia.Dispose();
                deck.VlcMedia = null;
            }

            deck.LoadedFilePath = null;
            deck.PendingSeekSeconds = null;

            var slider = GetSeekSlider(deck);
            var timeLabel = GetSeekTimeLabel(deck);
            var toggle = GetPlayToggle(deck);
            var label = GetNowLabel(deck);

            SafeUpdateSlider(slider, 0, 1, 0);
            UpdateSeekTimeLabel(0, timeLabel);
            SetPlayToggleState(toggle, false);
            toggle.Content = "⏵";
            UpdateDeckNowLabel(deck, label);
        }

        private void UpdateDeckNowLabel(DeckState deck, TextBlock label)
        {
            if (deck.CurrentIndex >= 0 && deck.CurrentIndex < deck.Queue.Count)
            {
                var text = $"Готов: {deck.Queue[deck.CurrentIndex].DisplayName}";
                if (deck.PendingSeekSeconds is double pending)
                {
                    text += $" ({FormatTime(pending)})";
                }
                label.Text = text;
            }
            else
            {
                label.Text = "Перетащите сюда трек";
            }
        }

        private static WaveStream CreateWaveStream(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Не указан путь к файлу", nameof(filePath));
            }

            if (Uri.TryCreate(filePath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return new MediaFoundationReader(filePath);
            }

            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

            if (string.IsNullOrEmpty(extension))
            {
                return new MediaFoundationReader(filePath);
            }

            return extension switch
            {
                ".ogg" => new VorbisWaveReader(filePath),
                ".wav" => new WaveFileReader(filePath),
                ".mp3" => new AudioFileReader(filePath),
                ".flac" => new MediaFoundationReader(filePath),
                ".wma" => new MediaFoundationReader(filePath),
                ".aac" => new MediaFoundationReader(filePath),
                ".m4a" => new MediaFoundationReader(filePath),
                _ => new MediaFoundationReader(filePath),
            };
        }

        private string BuildMetaKey(string filePath)
        {
            try
            {
                var rel = Path.GetRelativePath(_musicDirectory, filePath);
                return rel.Replace('\\', '/');
            }
            catch
            {
                return Path.GetFileName(filePath) ?? filePath;
            }
        }

        private double GetSavedVolume(string key, double fallback)
        {
            if (WindowSettingsManager.TryGetDouble(WindowKey, key, out var v))
            {
                return Math.Clamp(v, 0.0, 1.0);
            }
            return fallback;
        }

        private void SetupMusicWatcherIfNeeded(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }
            if (string.Equals(_watchedDirectory, directory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_musicWatcher != null)
            {
                try
                {
                    _musicWatcher.EnableRaisingEvents = false;
                    _musicWatcher.Created -= OnMusicFolderChanged;
                    _musicWatcher.Changed -= OnMusicFolderChanged;
                    _musicWatcher.Deleted -= OnMusicFolderChanged;
                    _musicWatcher.Renamed -= OnMusicFolderRenamed;
                    _musicWatcher.Dispose();
                }
                catch { }
                finally { _musicWatcher = null; }
            }

            _watchedDirectory = directory;
            try
            {
                var watcher = new FileSystemWatcher(directory)
                {
                    IncludeSubdirectories = true,
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                };
                watcher.Created += OnMusicFolderChanged;
                watcher.Changed += OnMusicFolderChanged;
                watcher.Deleted += OnMusicFolderChanged;
                watcher.Renamed += OnMusicFolderRenamed;
                watcher.EnableRaisingEvents = true;
                _musicWatcher = watcher;
            }
            catch
            {
                // ignore watcher failures
            }
        }

        private void OnMusicFolderChanged(object sender, FileSystemEventArgs e)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            void ScheduleRefresh()
            {
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                {
                    return;
                }

                _playlistRefreshScheduled = true;
                if (!_playlistRefreshTimer.IsEnabled)
                {
                    _playlistRefreshTimer.Start();
                }
            }

            if (Dispatcher.CheckAccess())
            {
                ScheduleRefresh();
                return;
            }

            try
            {
                Dispatcher.BeginInvoke((Action)ScheduleRefresh, DispatcherPriority.Background);
            }
            catch (InvalidOperationException)
            {
                // Dispatcher is shutting down; ignore.
            }
        }

        private void OnMusicFolderRenamed(object sender, RenamedEventArgs e)
        {
            OnMusicFolderChanged(sender, e);
        }

        private void PlaylistRefreshTimerOnTick(object? sender, EventArgs e)
        {
            if (!_playlistRefreshScheduled)
            {
                _playlistRefreshTimer.Stop();
                return;
            }
            _playlistRefreshScheduled = false;
            _playlistRefreshTimer.Stop();
            LoadPlaylist();
            ApplyFilter();
        }

        private void Splitter_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var cols = MainColumnsGrid.ColumnDefinitions;
                var left = cols[0];
                _leftSavedWidth = left.Width;
                _leftCollapsed = left.Width.Value <= 0.1;
                WindowSettingsManager.SetDouble(WindowKey, "LeftColumnWidth", left.ActualWidth);
                var total = Math.Max(1.0, MainColumnsGrid.ActualWidth - cols[1].ActualWidth);
                var ratio = Math.Max(0.0, Math.Min(1.0, left.ActualWidth / total));
                WindowSettingsManager.SetDouble(WindowKey, "LeftColumnRatio", ratio);
                _lastLeftRatio = ratio;
                SetStarColumnsByRatio(ratio);

                if (cols.Count > 2)
                {
                    var right = cols[2];
                    _rightSavedWidth = right.Width;
                    _rightCollapsed = right.Width.Value <= 0.1;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void Splitter_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            try
            {
                // Save again after layout settles
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var left = MainColumnsGrid.ColumnDefinitions[0];
                    WindowSettingsManager.SetDouble(WindowKey, "LeftColumnWidth", left.ActualWidth);
                    var total = Math.Max(1.0, MainColumnsGrid.ActualWidth - MainColumnsGrid.ColumnDefinitions[1].ActualWidth);
                    var ratio = Math.Max(0.0, Math.Min(1.0, left.ActualWidth / total));
                    WindowSettingsManager.SetDouble(WindowKey, "LeftColumnRatio", ratio);
                    _lastLeftRatio = ratio;
                    SetStarColumnsByRatio(ratio);
                }), DispatcherPriority.Background);
            }
            catch
            {
                // ignore
            }
        }

        private void SetStarColumnsByRatio(double ratio)
        {
            try
            {
                var cols = MainColumnsGrid.ColumnDefinitions;
                if (cols.Count < 3)
                {
                    return;
                }
                var left = cols[0];
                var right = cols[2];

                if (ratio <= 0.0001 || _leftCollapsed)
                {
                    left.MinWidth = 0;
                    left.Width = new GridLength(0);
                    right.Width = new GridLength(1.0, GridUnitType.Star);
                    _leftCollapsed = true;
                    return;
                }

                _leftCollapsed = false;
                var clamped = Math.Clamp(ratio, 0.01, 0.99);
                left.MinWidth = _leftDefaultMinWidth;
                left.Width = new GridLength(clamped, GridUnitType.Star);
                right.Width = new GridLength(1.0 - clamped, GridUnitType.Star);
                _lastLeftRatio = clamped;
            }
            catch { }
        }

        private void Splitter_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            try
            {
                var cols = MainColumnsGrid.ColumnDefinitions;
                if (cols.Count < 3)
                {
                    return;
                }

                var left = cols[0];
                var splitter = cols[1];
                var right = cols[2];

                var total = Math.Max(0.0, MainColumnsGrid.ActualWidth - splitter.ActualWidth);
                var minLeft = Math.Max(0.0, left.MinWidth);
                var minRight = Math.Max(0.0, right.MinWidth);

                var leftCurrent = left.ActualWidth;
                var targetLeft = leftCurrent + e.HorizontalChange;

                // clamp within [minLeft, total - minRight]
                var maxLeft = Math.Max(minLeft, total - minRight);
                targetLeft = Math.Max(minLeft, Math.Min(maxLeft, targetLeft));

                // apply pixel widths to keep splitter within bounds during drag
                left.Width = new GridLength(targetLeft);
                right.Width = new GridLength(Math.Max(minRight, total - targetLeft));
                e.Handled = true;
            }
            catch
            {
                // ignore
            }
        }

        private void LeftSplitterToggle_OnClick(object sender, RoutedEventArgs e)
        {
            if (_leftCollapsed)
            {
                ExpandLeft();
            }
            else
            {
                CollapseLeft();
            }
        }

        private void RightSplitterToggle_OnClick(object sender, RoutedEventArgs e)
        {
            if (_rightCollapsed)
            {
                ExpandRight();
            }
            else
            {
                CollapseRight();
            }
        }

        private void CollapseLeft()
        {
            var cols = MainColumnsGrid.ColumnDefinitions;
            var left = cols[0];
            _leftSavedWidth = left.Width.Value <= 0 ? (_leftSavedWidth.Value > 0 ? _leftSavedWidth : _leftDefaultWidth) : left.Width;
            left.MinWidth = 0;
            left.Width = new GridLength(0);
            _leftCollapsed = true;
            WindowSettingsManager.SetDouble(WindowKey, "LeftColumnWidth", 0);
            WindowSettingsManager.SetDouble(WindowKey, "LeftColumnRatio", 0);
            _lastLeftRatio = 0;
        }

        private void ExpandLeft()
        {
            var cols = MainColumnsGrid.ColumnDefinitions;
            var left = cols[0];
            left.MinWidth = _leftDefaultMinWidth;
            left.Width = _leftSavedWidth.Value > 0 ? _leftSavedWidth : _leftDefaultWidth;
            _leftCollapsed = false;
            WindowSettingsManager.SetDouble(WindowKey, "LeftColumnWidth", left.ActualWidth);
            var total = Math.Max(1.0, MainColumnsGrid.ActualWidth - cols[1].ActualWidth);
            var ratio = Math.Max(0.0, Math.Min(1.0, left.ActualWidth / total));
            WindowSettingsManager.SetDouble(WindowKey, "LeftColumnRatio", ratio);
            _lastLeftRatio = ratio;
        }

        private void CollapseRight()
        {
            var cols = MainColumnsGrid.ColumnDefinitions;
            if (cols.Count < 3)
            {
                return;
            }
            var right = cols[2];
            _rightSavedWidth = right.Width.Value <= 0 ? (_rightSavedWidth.Value > 0 ? _rightSavedWidth : _rightDefaultWidth) : right.Width;
            right.MinWidth = 0;
            right.Width = new GridLength(0);
            _rightCollapsed = true;
        }

        private void ExpandRight()
        {
            var cols = MainColumnsGrid.ColumnDefinitions;
            if (cols.Count < 3)
            {
                return;
            }
            var right = cols[2];
            right.MinWidth = _rightDefaultMinWidth;
            right.Width = _rightSavedWidth.Value > 0 ? _rightSavedWidth : _rightDefaultWidth;
            _rightCollapsed = false;
        }

        private void SaveDeckState()
        {
            // persistence will be implemented in a subsequent step
        }

        private void PlayPlaylistItemOnDeck(DeckState deck, PlaylistItem item, double? startSeconds = null, bool autoPlay = true)
        {
            var deckName = GetDeckName(deck);
            var sameTrackLoaded = deck.UseVlc
                ? deck.StreamUrl != null && string.Equals(deck.StreamUrl, item.FilePath, StringComparison.OrdinalIgnoreCase)
                : deck.Stream != null &&
                  deck.LoadedFilePath != null &&
                  string.Equals(deck.LoadedFilePath, item.FilePath, StringComparison.OrdinalIgnoreCase);

            double? seekSeconds = startSeconds.HasValue ? Math.Max(0, startSeconds.Value) : (double?)null;

            LogDebug($"PlayPlaylistItemOn{deckName} item={item.DisplayName}, seek={seekSeconds?.ToString("F3") ?? "null"}, sameLoaded={sameTrackLoaded}, autoPlay={autoPlay}");

            var slider = GetSeekSlider(deck);
            var toggle = GetPlayToggle(deck);
            var label = GetNowLabel(deck);
            var onStopped = GetPlaybackStoppedHandler(deck);

            if (sameTrackLoaded)
            {
                if (seekSeconds.HasValue)
                {
                    SeekDeck(deck, seekSeconds.Value, label);
                }

                if (autoPlay)
                {
                    if (deck.UseVlc)
                    {
                        if (deck.VlcPlayer != null && !deck.VlcPlayer.IsPlaying)
                        {
                            deck.VlcPlayer.Play();
                        }
                    }
                    else if (deck.WaveOut != null && deck.WaveOut.PlaybackState != PlaybackState.Playing)
                    {
                        deck.WaveOut.Play();
                    }

                    deck.IsPlaying = true;
                    deck.PendingSeekSeconds = null;
                    deck.ResumePosition = GetSliderPosition(slider);
                    SetPlayToggleState(toggle, true);
                }
                else
                {
                    if (deck.UseVlc)
                    {
                        deck.VlcPlayer?.Pause();
                    }
                    else if (deck.WaveOut != null && deck.WaveOut.PlaybackState == PlaybackState.Playing)
                    {
                        deck.WaveOut.Pause();
                    }

                    deck.IsPlaying = false;
                    deck.PendingSeekSeconds = null;
                    deck.ResumePosition = GetSliderPosition(slider);
                    SetPlayToggleState(toggle, false);
                }

                UpdateDeckNowLabel(deck, label);
                SaveDeckState();
                LogDebug(autoPlay ? $"{deckName} resumed existing track after seek" : $"{deckName} updated existing track without autoplay");
                return;
            }

            StopDeck(deck);
            LogDebug($"{deckName} stopped current playback for new item");

            deck.PendingSeekSeconds = seekSeconds;

            deck.Queue.Clear();
            deck.Queue.Add(item);
            deck.CurrentIndex = 0;

            UpdateDeckNowLabel(deck, label);

            if (seekSeconds.HasValue)
            {
                SeekDeck(deck, seekSeconds.Value, label);
            }

            if (autoPlay)
            {
                PlayOrResume(deck, label, onStopped);

                deck.IsPlaying = true;
                deck.ResumePosition = GetSliderPosition(slider);
                SetPlayToggleState(toggle, true);
                LogDebug($"{deckName} loaded new track and started playback");
            }
            else
            {
                deck.IsPlaying = false;
                deck.ResumePosition = GetSliderPosition(slider);
                SetPlayToggleState(toggle, false);
                UpdateDeckNowLabel(deck, label);
                LogDebug($"{deckName} prepared new track without autoplay");
            }

            SaveDeckState();
        }

        private void PlayPlaylistItemOnDeck1(PlaylistItem item, double? startSeconds = null, bool autoPlay = true) =>
            PlayPlaylistItemOnDeck(_deck1, item, startSeconds, autoPlay);

        private void PlayPlaylistItemOnDeck2(PlaylistItem item, double? startSeconds = null, bool autoPlay = true) =>
            PlayPlaylistItemOnDeck(_deck2, item, startSeconds, autoPlay);

        private void PlayPlaylistItemOnDeck3(PlaylistItem item, double? startSeconds = null, bool autoPlay = true) =>
            PlayPlaylistItemOnDeck(_deck3, item, startSeconds, autoPlay);

        private void PlayPlaylistItemOnDeck4(PlaylistItem item, double? startSeconds = null, bool autoPlay = true) =>
            PlayPlaylistItemOnDeck(_deck4, item, startSeconds, autoPlay);

        private void PlayYouTubeTrackOnDeck(DeckState deck, YouTubeTrack track, double? startSeconds = null, bool autoPlay = true)
        {
            _ = PlayYouTubeTrackOnDeckAsync(deck, track, startSeconds, autoPlay);
        }

        private async Task PlayYouTubeTrackOnDeckAsync(DeckState deck, YouTubeTrack track, double? startSeconds, bool autoPlay)
        {
            try
            {
                LogDebug($"Preparing YouTube track '{track.DisplayName}' for playback on deck...");
                PlaylistItem item;
                try
                {
                    item = await Task.Run(() => CreatePlaylistItemFromYouTube(track, allowPreviewFallback: false));
                }
                catch (InvalidOperationException)
                {
                    item = await Task.Run(() => CreatePlaylistItemFromYouTube(track, allowPreviewFallback: true));
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    PlayPlaylistItemOnDeck(deck, item, startSeconds, autoPlay);
                    _selectedYouTubeTrack = track;
                });

                _ = Task.Run(() => EnsureSegmentWindowAsync(track, 0));

                if (item.IsYouTubePreview)
                {
                    _ = Task.Run(() => UpgradePreviewToBufferedPlaybackAsync(deck, track));
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(this, $"Не удалось воспроизвести YouTube-трек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void PlayPlaylistItemOnDeck1(PlaylistItem item, bool autoPlay)
        {
            PlayPlaylistItemOnDeck(_deck1, item, null, autoPlay);
        }

        private void RenderDescriptionTextBlock(TextBlock textBlock, string? text)
        {
            // implementation of RenderDescriptionTextBlock method
            textBlock.Inlines.Clear();

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            int index = 0;
            foreach (Match match in TimecodeRegex.Matches(text))
            {
                if (match.Index > index)
                {
                    AppendPlainText(textBlock, text.Substring(index, match.Index - index));
                }

                if (TryGetSeconds(match, out var seconds))
                {
                    var hyperlink = new Hyperlink(new Run(match.Value))
                    {
                        Foreground = TimecodeLinkBrush,
                        Cursor = Cursors.Hand,
                        TextDecorations = TextDecorations.Underline
                    };
                    hyperlink.Tag = new TimecodeLinkData(match.Value, seconds);
                    hyperlink.DataContext = textBlock.DataContext;
                    hyperlink.Click += DescriptionTimecodeHyperlink_OnClick;
                    hyperlink.PreviewMouseLeftButtonDown += DescriptionTimecodeHyperlink_OnPreviewMouseLeftButtonDown;
                    hyperlink.PreviewMouseLeftButtonUp += DescriptionTimecodeHyperlink_OnPreviewMouseLeftButtonUp;
                    hyperlink.PreviewMouseMove += DescriptionTimecodeHyperlink_OnPreviewMouseMove;
                    textBlock.Inlines.Add(hyperlink);
                }
                else
                {
                    AppendPlainText(textBlock, match.Value);
                }

                index = match.Index + match.Length;
            }

            if (index < text.Length)
            {
                AppendPlainText(textBlock, text.Substring(index));
            }
        }

        private static bool TryGetSeconds(Match match, out double seconds)
        {
            seconds = 0;
            if (!match.Success)
            {
                return false;
            }

            var minuteGroup = match.Groups["minutes"];
            var secondGroup = match.Groups["seconds"];
            if (!minuteGroup.Success || !secondGroup.Success)
            {
                return false;
            }

            if (!int.TryParse(minuteGroup.Value, out var minutes))
            {
                return false;
            }
            if (!int.TryParse(secondGroup.Value, out var secs))
            {
                return false;
            }

            if (secs >= 60 || secs < 0)
            {
                return false;
            }

            int hours = 0;
            var hourGroup = match.Groups["hours"];
            if (hourGroup.Success && !int.TryParse(hourGroup.Value, out hours))
            {
                return false;
            }

            seconds = hours * 3600 + minutes * 60 + secs;
            return true;
        }

        private static void AppendPlainText(TextBlock textBlock, string segment)
        {
            if (string.IsNullOrEmpty(segment))
            {
                return;
            }

            var lines = segment.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    textBlock.Inlines.Add(new LineBreak());
                }

                if (!string.IsNullOrEmpty(lines[i]))
                {
                    textBlock.Inlines.Add(new Run(lines[i]));
                }
            }

            if (segment.EndsWith("\r") || segment.EndsWith("\n"))
            {
                textBlock.Inlines.Add(new LineBreak());
            }
        }

        private static SolidColorBrush CreateTimecodeBrush()
        {
            var brush = new SolidColorBrush(Color.FromRgb(0x2A, 0x85, 0xFF));
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }
            return brush;
        }

        private sealed record TimecodeLinkData(string Token, double Seconds);

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T typed)
                {
                    return typed;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void InitAudioDevices()
        {
            _isPopulatingDevices = true;
            try
            {
                _outputDevices = new List<DeviceItem>();
                _outputDevices.Add(new DeviceItem(-1, "По умолчанию"));
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var caps = WaveOut.GetCapabilities(i);
                    _outputDevices.Add(new DeviceItem(i, caps.ProductName));
                }

                Deck1DeviceCombo.DisplayMemberPath = "Name";
                Deck1DeviceCombo.SelectedValuePath = "Index";
                Deck1DeviceCombo.ItemsSource = _outputDevices;
                Deck1DeviceCombo.SelectedValue = _deck1.DeviceNumber;

                Deck2DeviceCombo.DisplayMemberPath = "Name";
                Deck2DeviceCombo.SelectedValuePath = "Index";
                Deck2DeviceCombo.ItemsSource = _outputDevices;
                Deck2DeviceCombo.SelectedValue = _deck2.DeviceNumber;

                Deck3DeviceCombo.DisplayMemberPath = "Name";
                Deck3DeviceCombo.SelectedValuePath = "Index";
                Deck3DeviceCombo.ItemsSource = _outputDevices;
                Deck3DeviceCombo.SelectedValue = _deck3.DeviceNumber;

                Deck4DeviceCombo.DisplayMemberPath = "Name";
                Deck4DeviceCombo.SelectedValuePath = "Index";
                Deck4DeviceCombo.ItemsSource = _outputDevices;
                Deck4DeviceCombo.SelectedValue = _deck4.DeviceNumber;
            }
            finally
            {
                _isPopulatingDevices = false;
            }
        }

        private void Deck1DeviceCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPopulatingDevices)
            {
                return;
            }
            var item = Deck1DeviceCombo.SelectedItem as DeviceItem;
            var deviceNumber = item?.Index ?? -1;
            if (deviceNumber == _deck1.DeviceNumber)
            {
                return;
            }
            _deck1.DeviceNumber = deviceNumber;
            ApplyDeviceToDeck(_deck1, Deck1NowLabel, OnDeck1PlaybackStopped);
        }

        private void Deck2DeviceCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPopulatingDevices)
            {
                return;
            }
            var item = Deck2DeviceCombo.SelectedItem as DeviceItem;
            var deviceNumber = item?.Index ?? -1;
            if (deviceNumber == _deck2.DeviceNumber)
            {
                return;
            }
            _deck2.DeviceNumber = deviceNumber;
            ApplyDeviceToDeck(_deck2, Deck2NowLabel, OnDeck2PlaybackStopped);
        }

        private void Deck3DeviceCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPopulatingDevices)
            {
                return;
            }
            var item = Deck3DeviceCombo.SelectedItem as DeviceItem;
            var deviceNumber = item?.Index ?? -1;
            if (deviceNumber == _deck3.DeviceNumber)
            {
                return;
            }
            _deck3.DeviceNumber = deviceNumber;
            ApplyDeviceToDeck(_deck3, Deck3NowLabel, OnDeck3PlaybackStopped);
        }

        private void Deck4DeviceCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPopulatingDevices)
            {
                return;
            }
            var item = Deck4DeviceCombo.SelectedItem as DeviceItem;
            var deviceNumber = item?.Index ?? -1;
            if (deviceNumber == _deck4.DeviceNumber)
            {
                return;
            }
            _deck4.DeviceNumber = deviceNumber;
            ApplyDeviceToDeck(_deck4, Deck4NowLabel, OnDeck4PlaybackStopped);
        }

        private void ApplyDeviceToDeck(DeckState deck, TextBlock label, EventHandler<StoppedEventArgs> onStopped)
        {
            bool wasPlaying = false;
            bool wasPaused = false;

            if (deck.UseVlc)
            {
                if (deck.VlcPlayer != null)
                {
                    wasPlaying = deck.VlcPlayer.IsPlaying;
                    wasPaused = deck.VlcPlayer.State == VLCState.Paused;
                }
            }
            else
            {
                if (deck.WaveOut != null)
                {
                    wasPlaying = deck.WaveOut.PlaybackState == PlaybackState.Playing;
                    wasPaused = deck.WaveOut.PlaybackState == PlaybackState.Paused;
                }
            }

            StopDeck(deck); // This will stop and dispose both VLC and NAudio players

            if (deck.Queue.Count == 0 || deck.CurrentIndex < 0 || deck.CurrentIndex >= deck.Queue.Count)
            {
                return;
            }

            var currentTrack = deck.Queue[deck.CurrentIndex];

            if (deck.UseVlc)
            {
                if (string.IsNullOrWhiteSpace(deck.StreamUrl))
                {
                    return;
                }

                var mediaOptions = new List<string>();
                foreach (var header in deck.AppliedHeaders)
                {
                    mediaOptions.Add($":http-header={header.Key}={header.Value}");
                }

                deck.VlcMedia = Vlc.CreateMedia(deck.StreamUrl, mediaOptions);
                deck.VlcPlayer = Vlc.CreateMediaPlayer();
                deck.VlcPlayer.Media = deck.VlcMedia;

                deck.VlcEndReachedHandler = (sender, args) => Dispatcher.Invoke(() => onStopped(sender, new StoppedEventArgs(null)));
                deck.VlcPlayer.Stopped += deck.VlcEndReachedHandler;
                deck.VlcErrorHandler = (sender, args) => Dispatcher.Invoke(() =>
                {
                    LogDebug($"VLC Error on {GetDeckName(deck)}: playback encountered an error.");
                    onStopped(sender, new StoppedEventArgs(null)); // Treat error as stop
                });
                deck.VlcPlayer.EncounteredError += deck.VlcErrorHandler;

                deck.VlcPlayer.Volume = (int)(deck.Volume * 100);

                if (wasPlaying)
                {
                    deck.VlcPlayer.Play();
                    label.Text = $"Играет: {currentTrack.DisplayName}";
                }
                else if (wasPaused)
                {
                    deck.VlcPlayer.Play(); // Play and then pause to set position
                    deck.VlcPlayer.Pause();
                    label.Text = $"Играет: {currentTrack.DisplayName}";
                }
                else
                {
                    UpdateDeckNowLabel(deck, label);
                }
            }
            else
            {
                if (deck.Stream == null || deck.VolumeProvider == null)
                {
                    // Re-create stream and volume provider if they were disposed by StopDeck
                    try
                    {
                        deck.Stream = CreateWaveStream(currentTrack.FilePath);
                        deck.VolumeProvider = new VolumeSampleProvider(deck.Stream.ToSampleProvider())
                        {
                            Volume = (float)Math.Clamp(deck.Volume, 0.0, 1.0)
                        };
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"ApplyDeviceToDeck: Failed to re-create NAudio stream for {currentTrack.DisplayName}: {ex.Message}");
                        return;
                    }
                }

                deck.WaveOut = new WaveOutEvent { DeviceNumber = deck.DeviceNumber };
                deck.WaveOut.PlaybackStopped += onStopped;
                deck.WaveOut.Init(deck.VolumeProvider);

                if (wasPlaying)
                {
                    deck.WaveOut.Play();
                    label.Text = $"Играет: {currentTrack.DisplayName}";
                }
                else if (wasPaused)
                {
                    deck.WaveOut.Play();
                    deck.WaveOut.Pause();
                    label.Text = $"Играет: {currentTrack.DisplayName}";
                }
                else
                {
                    UpdateDeckNowLabel(deck, label);
                }
            }
        }

        public class PlaylistItem
        {
            public string DisplayName { get; }
            public string FilePath { get; }
            public string? Description { get; set; }
            public List<string> Tags { get; set; }
            public string TagsDisplay => Tags.Count > 0 ? string.Join(", ", Tags) : string.Empty;
            public ImageSource? CoverImage { get; }
            public bool IsYouTubeStream { get; set; }
            public List<KeyValuePair<string, string>> StreamHeaders { get; set; } = new();
            public string? VideoId { get; set; }
            public YouTubeTrack? SourceYouTubeTrack { get; set; }
            public bool IsStreaming => IsYouTubeStream && !string.IsNullOrWhiteSpace(FilePath) &&
                                       Uri.TryCreate(FilePath, UriKind.Absolute, out var uri) &&
                                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            public bool IsYouTubePreview { get; set; }

            public PlaylistItem(string displayName, string filePath, string? description, List<string> tags, ImageSource? coverImage)
            {
                DisplayName = displayName;
                FilePath = filePath;
                Description = description;
                Tags = tags;
                CoverImage = coverImage;
            }
        }

        public class YouTubeTrack : INotifyPropertyChanged
        {
            public YouTubeTrack(string displayName, string videoId, string? description, string? streamUrl, double? durationSeconds, string? previewPath, string? bufferPath)
            {
                DisplayName = displayName;
                VideoId = videoId;
                Description = description;
                StreamUrl = streamUrl;
                DurationSeconds = durationSeconds;
                PreviewPath = previewPath;
                BufferPath = bufferPath;
                UpdateDownloadedState();
            }

            public string DisplayName { get; }
            public string VideoId { get; }
            public string? Description { get; }
            public string? StreamUrl { get; private set; }
            public double? DurationSeconds { get; }
            public string? PreviewPath { get; private set; }
            public string? BufferPath { get; private set; }
            public List<KeyValuePair<string, string>> StreamHeaders { get; private set; } = new();
            public string DurationDisplay => DurationSeconds.HasValue ? FormatTime(DurationSeconds.Value) : string.Empty;
            public bool IsDownloaded { get; private set; }
            public bool IsDownloading { get; private set; }
            public double DownloadProgress { get; private set; }
            public bool IsNotDownloaded => !IsDownloading && !IsDownloaded;
            public bool IsPreviewOnly { get; private set; }
            public bool IsBufferDownloading { get; private set; }
            public double BufferDownloadProgress { get; private set; }
            public string DownloadStatusIcon => IsDownloaded ? "✔" : IsDownloading ? "⬇" : "✖";
            public string DownloadStatusText => IsDownloaded
                ? "Скачано"
                : IsDownloading
                    ? $"Скачивается {DownloadProgress:0}%"
                    : "Не скачано";
            public event PropertyChangedEventHandler? PropertyChanged;

            public void UpdateStreamUrl(string? streamUrl)
            {
                if (!string.Equals(StreamUrl, streamUrl, StringComparison.OrdinalIgnoreCase))
                {
                    StreamUrl = streamUrl;
                    OnPropertyChanged(nameof(StreamUrl));
                    UpdateDownloadedState();
                }
            }

            public void UpdateStreamHeaders(List<KeyValuePair<string, string>> headers)
            {
                StreamHeaders = headers ?? new List<KeyValuePair<string, string>>();
                OnPropertyChanged(nameof(StreamHeaders));
            }

            public void UpdatePreviewPath(string? previewPath)
            {
                if (!string.Equals(PreviewPath, previewPath, StringComparison.OrdinalIgnoreCase))
                {
                    PreviewPath = previewPath;
                    OnPropertyChanged(nameof(PreviewPath));
                }
            }

            public void UpdateBufferPath(string? bufferPath)
            {
                if (!string.Equals(BufferPath, bufferPath, StringComparison.OrdinalIgnoreCase))
                {
                    BufferPath = bufferPath;
                    OnPropertyChanged(nameof(BufferPath));
                }
            }

            private void UpdateDownloadedState()
            {
                var wasDownloaded = IsDownloaded;
                var isLocal = !string.IsNullOrWhiteSpace(StreamUrl) && TryGetLocalPathFromStreamUrl(StreamUrl, out _);
                IsDownloaded = isLocal;
                if (IsDownloaded != wasDownloaded)
                {
                    OnPropertyChanged(nameof(IsDownloaded));
                    OnPropertyChanged(nameof(IsNotDownloaded));
                    NotifyStatusChanged();
                }

                if (IsDownloaded && DownloadProgress < 100)
                {
                    DownloadProgress = 100;
                    OnPropertyChanged(nameof(DownloadProgress));
                    NotifyStatusChanged();
                }
                else if (!IsDownloaded && !IsDownloading && DownloadProgress > 0)
                {
                    DownloadProgress = 0;
                    OnPropertyChanged(nameof(DownloadProgress));
                    NotifyStatusChanged();
                }
            }

            public void SetDownloading(bool value)
            {
                if (IsDownloading == value)
                {
                    return;
                }

                IsDownloading = value;
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(IsNotDownloaded));
                NotifyStatusChanged();

                if (!IsDownloading && !IsDownloaded && DownloadProgress > 0)
                {
                    DownloadProgress = 0;
                    OnPropertyChanged(nameof(DownloadProgress));
                    NotifyStatusChanged();
                }
            }

            public void SetDownloadProgress(double value)
            {
                var clamped = Math.Clamp(value, 0, 100);
                if (Math.Abs(DownloadProgress - clamped) < 0.1)
                {
                    return;
                }

                DownloadProgress = clamped;
                OnPropertyChanged(nameof(DownloadProgress));
                NotifyStatusChanged();
            }

            private void NotifyStatusChanged()
            {
                OnPropertyChanged(nameof(DownloadStatusIcon));
                OnPropertyChanged(nameof(DownloadStatusText));
            }

            private void OnPropertyChanged(string propertyName)
            {
                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
                }
                else
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private class YouTubeTrackStorage
        {
            public string? Title { get; set; }
            public string? VideoId { get; set; }
            public string? Description { get; set; }
            public string? StreamUrl { get; set; }
            public double? DurationSeconds { get; set; }
            public string? PreviewPath { get; set; }
            public string? BufferPath { get; set; }

            public static List<YouTubeTrackStorage> Load()
            {
                try
                {
                    if (!System.IO.File.Exists(YouTubeTracksFilePath))
                    {
                        return new List<YouTubeTrackStorage>();
                    }

                    var json = System.IO.File.ReadAllText(YouTubeTracksFilePath);
                    var list = JsonSerializer.Deserialize<List<YouTubeTrackStorage>>(json, YouTubeTracksSerializerOptions);
                    return list ?? new List<YouTubeTrackStorage>();
                }
                catch (Exception ex)
                {
                    LogDebug($"YouTubeTrackStorage.Load failed: {ex.Message}");
                    return new List<YouTubeTrackStorage>();
                }
            }

            public static void Write(List<YouTubeTrackStorage> entries)
            {
                LogDebug($"YouTubeTrackStorage.Write persisting {entries.Count} entries");
                var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                System.IO.File.WriteAllText(YouTubeTracksFilePath, json, Encoding.UTF8);
            }
        }

        private static YouTubeTrackStorage? FetchYouTubeMetadata(string link)
        {
            try
            {
                if (!System.IO.File.Exists(ExternalToolInstaller.YtDlpPath))
                {
                    LogDebug("FetchYouTubeMetadata: yt-dlp not found");
                    return null;
                }

                var command = BuildYtDlpMetadataCommand(link);

                if (!TryRunYtDlp(command, out var output, out _))
                {
                    return null;
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    LogDebug("yt-dlp returned empty output.");
                    return null;
                }

                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;

                string? title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
                string? videoId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                string? description = root.TryGetProperty("description", out var descElement) ? descElement.GetString() : null;
                double? durationSeconds = null;
                if (root.TryGetProperty("duration", out var durationElement) && durationElement.ValueKind == JsonValueKind.Number && durationElement.TryGetDouble(out var dur))
                {
                    durationSeconds = dur;
                }

                string? streamUrl = TryExtractStreamUrl(root);
                string? streamExtension = ExtractExtensionFromUrl(streamUrl);
                string? streamCodec = ExtractAudioCodec(root, streamUrl);

                if (string.IsNullOrWhiteSpace(videoId))
                {
                    videoId = ExtractYouTubeVideoId(link);
                }

                streamUrl = SanitizeStreamUrl(streamUrl, streamExtension, streamCodec, root);
                streamExtension = ExtractExtensionFromUrl(streamUrl);
                streamCodec = ExtractAudioCodec(root, streamUrl);

                if (string.IsNullOrWhiteSpace(streamUrl) || !IsPlayable(streamExtension, streamCodec))
                {
                    streamUrl = link;
                    streamExtension = null;
                    streamCodec = null;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "YouTube трек";
                }

                var storage = new YouTubeTrackStorage
                {
                    Title = title,
                    VideoId = videoId,
                    Description = description,
                    StreamUrl = streamUrl,
                    DurationSeconds = durationSeconds,
                    PreviewPath = ScheduleYouTubePreviewDownload(videoId, link),
                    BufferPath = null
                };

                return storage;
            }
            catch (Exception ex)
            {
                LogDebug($"FetchYouTubeMetadata exception: {ex.Message}");
                return null;
            }
        }

        private static string? ScheduleYouTubePreviewDownload(string? videoId, string? link)
        {
            if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(link))
            {
                return null;
            }

            try
            {
                var previewFile = Path.Combine(EnsureYouTubeCacheDirectory(), $"{videoId}{YouTubePreviewSuffix}.m4a");
                if (IOFile.Exists(previewFile))
                {
                    return previewFile;
                }

                if (!PreviewDownloadsInFlight.TryAdd(videoId, 0))
                {
                    return previewFile;
                }

                Task.Run(() =>
                {
                    try
                    {
                        DownloadPreviewSnippet(link, videoId, previewFile);
                        if (IOFile.Exists(previewFile))
                        {
                            UpdateYouTubeTrackStoragePreviewPath(videoId, previewFile);
                        }
                    }
                    finally
                    {
                        PreviewDownloadsInFlight.TryRemove(videoId, out _);
                    }
                });

                return previewFile;
            }
            catch (Exception ex)
            {
                LogDebug($"ScheduleYouTubePreviewDownload failed for {videoId}: {ex.Message}");
                return null;
            }
        }

        private static void DownloadPreviewSnippet(string link, string videoId, string destination)
        {
            try
            {
                LogDebug($"DownloadPreviewSnippet start for {videoId} -> {destination}");
                var command = BuildYtDlpPreviewCommand(link, videoId, destination);
                if (!TryRunYtDlp(command, out _, out _))
                {
                    LogDebug($"DownloadPreviewSnippet yt-dlp failed for {videoId}");
                    return;
                }

                if (IOFile.Exists(destination))
                {
                    LogDebug($"DownloadPreviewSnippet success for {videoId}, trimming");
                    TrimPreviewSnippet(destination, PreviewDurationSeconds);
                    return;
                }
                LogDebug($"DownloadPreviewSnippet missing output for {videoId}");
            }
            catch (Exception ex)
            {
                LogDebug($"DownloadPreviewSnippet failed for {videoId}: {ex.Message}");
            }
        }

        private static bool DownloadBufferSnippet(string link, string videoId, string destination)
        {
            try
            {
                LogDebug($"DownloadBufferSnippet start for {videoId} -> {destination}");
                var command = BuildYtDlpBufferCommand(link, videoId, destination);
                var commandSucceeded = TryRunYtDlp(command, out _, out _);

                if (!IOFile.Exists(destination))
                {
                    if (!commandSucceeded)
                    {
                        LogDebug($"DownloadBufferSnippet yt-dlp failed for {videoId} (no output)");
                    }
                    else
                    {
                        LogDebug($"DownloadBufferSnippet missing output for {videoId}");
                    }
                    return false;
                }

                if (!commandSucceeded)
                {
                    LogDebug($"DownloadBufferSnippet detected output despite yt-dlp error for {videoId}, treating as success.");
                }

                TrimPreviewSnippet(destination, BufferedTotalSeconds);
                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"DownloadBufferSnippet failed for {videoId}: {ex.Message}");
            }

            return false;
        }

        private static void TrimPreviewSnippet(string sourcePath, int maxSeconds)
        {
            try
            {
                if (maxSeconds <= 0 || string.IsNullOrWhiteSpace(sourcePath) || !IOFile.Exists(sourcePath))
                {
                    return;
                }

                if (!IOFile.Exists(ExternalToolInstaller.FfmpegPath))
                {
                    LogDebug("TrimPreviewSnippet: ffmpeg not found, skipping trim.");
                    return;
                }

                var tempPath = sourcePath + ".trimmed";
                if (IOFile.Exists(tempPath))
                {
                    IOFile.Delete(tempPath);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = ExternalToolInstaller.FfmpegPath,
                    Arguments = $"-y -i \"{sourcePath}\" -t {maxSeconds} -c copy \"{tempPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    LogDebug("TrimPreviewSnippet: failed to start ffmpeg.");
                    return;
                }

                var stderr = process.StandardError.ReadToEndAsync();
                var stdout = process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();
                Task.WaitAll(stdout, stderr);

                if (process.ExitCode != 0 || !IOFile.Exists(tempPath))
                {
                    LogDebug($"TrimPreviewSnippet: ffmpeg exited with {process.ExitCode}: {stderr.Result}");
                    return;
                }

                IOFile.Delete(sourcePath);
                IOFile.Move(tempPath, sourcePath);
            }
            catch (Exception ex)
            {
                LogDebug($"TrimPreviewSnippet failed: {ex.Message}");
            }
        }

        private static YtDlpCommand BuildYtDlpPreviewCommand(string link, string videoId, string destination)
        {
            var baseArgs = new StringBuilder();
            baseArgs.Append("--no-playlist --force-overwrites --newline ");
            baseArgs.Append("--remux-video mp4 ");
            baseArgs.Append("--extract-audio --audio-format m4a --audio-quality 0 ");
            baseArgs.Append("--download-sections \"*0-" + PreviewDurationSeconds + "\" ");
            baseArgs.Append("--max-downloads 1 --force-ipv4 --socket-timeout 20 ");
            var cookiesArg = GetCookiesArgument(out var usesCookieFile);
            baseArgs.Append(cookiesArg);
            AppendAuthorizationHeaders(baseArgs, link);

            var variants = new List<CommandVariant>();

            if (usesCookieFile)
            {
                LogDebug($"BuildYtDlpPreviewCommand cookies-enabled for {videoId}");
                var defaultPrimary = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultPrimary);
                AppendHlsFormatArgs(defaultPrimary);
                defaultPrimary.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultPrimary.ToString().Trim(), null));

                var defaultDirect = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultDirect);
                defaultDirect.Append("--format bestaudio[ext=m4a]/bestaudio ");
                defaultDirect.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultDirect.ToString().Trim(), "Applying yt-dlp fallback (default client direct preview)."));
            }
            else
            {
                LogDebug($"BuildYtDlpPreviewCommand cookies-disabled for {videoId}");
                var androidMusicPrimary = new StringBuilder(baseArgs.ToString());
                androidMusicPrimary.Append("--format bestaudio[ext=m4a]/bestaudio ");
                AppendAndroidMusicClientArgs(androidMusicPrimary, includeFormatAndHls: false);
                androidMusicPrimary.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(androidMusicPrimary.ToString().Trim(), null));

                var androidHls = new StringBuilder(baseArgs.ToString());
                AppendAndroidClientArgs(androidHls);
                AppendHlsFormatArgs(androidHls);
                androidHls.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(androidHls.ToString().Trim(), "Applying yt-dlp fallback (android client HLS preview)."));

                var defaultHls = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultHls);
                AppendHlsFormatArgs(defaultHls);
                defaultHls.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultHls.ToString().Trim(), "Applying yt-dlp fallback (default client HLS preview)."));
            }

            return new YtDlpCommand(link, variants, usesCookieFile: usesCookieFile);
        }

        private static YtDlpCommand BuildYtDlpSegmentCommand(string link, string videoId, int segmentIndex, string destination)
        {
            var startSeconds = Math.Max(0, segmentIndex) * SegmentDurationSeconds;
            var endSeconds = startSeconds + SegmentDurationSeconds;

            var baseArgs = new StringBuilder();
            baseArgs.Append("--no-playlist --force-overwrites --newline ");
            baseArgs.Append("--remux-video mp4 ");
            baseArgs.Append("--extract-audio --audio-format m4a --audio-quality 0 ");
            baseArgs.Append("--download-sections \"*" + startSeconds + "-" + endSeconds + "\" ");
            baseArgs.Append("--no-part ");
            baseArgs.Append("--abort-on-error ");
            baseArgs.Append("--max-downloads 1 --force-ipv4 --socket-timeout 20 ");
            var cookiesArg = GetCookiesArgument(out var usesCookieFile);
            baseArgs.Append(cookiesArg);
            AppendAuthorizationHeaders(baseArgs, link);

            var variants = new List<CommandVariant>();

            if (usesCookieFile)
            {
                var defaultPrimary = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultPrimary);
                AppendHlsFormatArgs(defaultPrimary);
                defaultPrimary.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultPrimary.ToString().Trim(), null));

                var defaultDirect = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultDirect);
                defaultDirect.Append("--format bestaudio[ext=m4a]/bestaudio ");
                defaultDirect.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultDirect.ToString().Trim(), "Applying yt-dlp fallback (default client direct segment)."));
            }
            else
            {
                LogDebug($"BuildYtDlpSegmentCommand cookies-disabled for {videoId} seg {segmentIndex}");
                var primary = new StringBuilder(baseArgs.ToString());
                primary.Append("--format bestaudio[ext=m4a]/bestaudio ");
                AppendAndroidMusicClientArgs(primary, includeFormatAndHls: false);
                primary.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(primary.ToString().Trim(), null));

                var androidFallback = new StringBuilder(baseArgs.ToString());
                AppendAndroidClientArgs(androidFallback);
                AppendHlsFormatArgs(androidFallback);
                androidFallback.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(androidFallback.ToString().Trim(), "Applying yt-dlp fallback (android client HLS segment)."));

                var defaultHls = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultHls);
                AppendHlsFormatArgs(defaultHls);
                defaultHls.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultHls.ToString().Trim(), "Applying yt-dlp fallback (default client HLS segment)."));
            }

            return new YtDlpCommand(link, variants, usesCookieFile: usesCookieFile);
        }

        private static YtDlpCommand BuildYtDlpBufferCommand(string link, string videoId, string destination)
        {
            var baseArgs = new StringBuilder();
            baseArgs.Append("--no-playlist --force-overwrites --newline ");
            baseArgs.Append("--remux-video mp4 ");
            baseArgs.Append("--extract-audio --audio-format m4a --audio-quality 0 ");
            baseArgs.Append("--download-sections \"*0-" + BufferedTotalSeconds + "\" ");
            baseArgs.Append("--max-downloads 1 --force-ipv4 --socket-timeout 20 ");
            var cookiesArg = GetCookiesArgument(out var usesCookieFile);
            baseArgs.Append(cookiesArg);
            AppendAuthorizationHeaders(baseArgs, link);

            var variants = new List<CommandVariant>();

            if (usesCookieFile)
            {
                var defaultPrimary = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultPrimary);
                AppendHlsFormatArgs(defaultPrimary);
                defaultPrimary.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultPrimary.ToString().Trim(), null));

                var defaultDirect = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultDirect);
                defaultDirect.Append("--format bestaudio[ext=m4a]/bestaudio ");
                defaultDirect.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultDirect.ToString().Trim(), "Applying yt-dlp fallback (default client direct buffer)."));
            }
            else
            {
                var androidMusicPrimary = new StringBuilder(baseArgs.ToString());
                androidMusicPrimary.Append("--format bestaudio[ext=m4a]/bestaudio ");
                AppendAndroidMusicClientArgs(androidMusicPrimary, includeFormatAndHls: false);
                androidMusicPrimary.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(androidMusicPrimary.ToString().Trim(), null));

                var androidHls = new StringBuilder(baseArgs.ToString());
                AppendAndroidClientArgs(androidHls);
                AppendHlsFormatArgs(androidHls);
                androidHls.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(androidHls.ToString().Trim(), "Applying yt-dlp fallback (android client HLS buffer)."));

                var defaultHls = new StringBuilder(baseArgs.ToString());
                AppendDefaultClientArgs(defaultHls);
                AppendHlsFormatArgs(defaultHls);
                defaultHls.Append("--output \"").Append(destination).Append("\" ");
                variants.Add(new(defaultHls.ToString().Trim(), "Applying yt-dlp fallback (default client HLS buffer)."));
            }

            return new YtDlpCommand(link, variants, usesCookieFile: usesCookieFile);
        }

        private static string EnsureYouTubeCacheDirectory()
        {
            try
            {
                Directory.CreateDirectory(YouTubeCacheDirectory);
            }
            catch (Exception ex)
            {
                LogDebug($"EnsureYouTubeCacheDirectory failed: {ex.Message}");
            }

            return YouTubeCacheDirectory;
        }

        private static string? ExtractExtensionFromUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var pathExtension = Path.GetExtension(uri.AbsolutePath);
                    if (!string.IsNullOrEmpty(pathExtension))
                    {
                        return NormalizeExtension(pathExtension.TrimStart('.'));
                    }

                    var decodedQuery = WebUtility.UrlDecode(uri.Query);
                    if (!string.IsNullOrEmpty(decodedQuery))
                    {
                        var match = Regex.Match(decodedQuery, @"mime=(?:audio|video)/([\w.+-]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            return NormalizeExtension(match.Groups[1].Value);
                        }
                    }
                }
            }
            catch
            {
                // ignore URI parsing issues
            }

            return null;
        }

        private static string? ExtractAudioCodec(JsonElement root, string? streamUrl)
        {
            if (!string.IsNullOrEmpty(streamUrl))
            {
                foreach (var candidate in EnumerateStreamCandidates(root))
                {
                    if (string.Equals(candidate.Url, streamUrl, StringComparison.Ordinal))
                    {
                        return candidate.Codec;
                    }
                }
            }
            else
            {
                foreach (var candidate in EnumerateStreamCandidates(root))
                {
                    if (!string.IsNullOrEmpty(candidate.Codec))
                    {
                        return candidate.Codec;
                    }
                }
            }

            return null;
        }

        private static string? SanitizeStreamUrl(string? streamUrl, string? streamExtension, string? streamCodec, JsonElement root)
        {
            var candidates = EnumerateStreamCandidates(root).ToList();

            if (!string.IsNullOrWhiteSpace(streamUrl) &&
                candidates.All(c => !string.Equals(c.Url, streamUrl, StringComparison.Ordinal)))
            {
                candidates.Insert(0, new YouTubeStreamCandidate(streamUrl, NormalizeExtension(streamExtension), NormalizeCodec(streamCodec)));
            }

            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate.Url, streamUrl, StringComparison.Ordinal) && IsAcceptable(candidate.Extension, candidate.Codec))
                {
                    return candidate.Url;
                }
            }

            foreach (var candidate in candidates)
            {
                if (IsAcceptable(candidate.Extension, candidate.Codec) && IsPreferred(candidate.Extension, candidate.Codec))
                {
                    return candidate.Url;
                }
            }

            foreach (var candidate in candidates)
            {
                if (IsAcceptable(candidate.Extension, candidate.Codec))
                {
                    return candidate.Url;
                }
            }

            return null;
        }

        private static IEnumerable<YouTubeStreamCandidate> EnumerateStreamCandidates(JsonElement root)
        {
            if (root.TryGetProperty("requested_downloads", out var requested) && requested.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in requested.EnumerateArray())
                {
                    var candidate = CreateCandidate(entry);
                    if (candidate != null)
                    {
                        yield return candidate;
                    }
                }
            }

            if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in formats.EnumerateArray().Reverse())
                {
                    var candidate = CreateCandidate(entry);
                    if (candidate != null)
                    {
                        yield return candidate;
                    }
                }
            }
        }

        private static YouTubeStreamCandidate? CreateCandidate(JsonElement entry)
        {
            if (!entry.TryGetProperty("url", out var urlElement))
            {
                return null;
            }

            var url = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var acodec = entry.TryGetProperty("acodec", out var acodecElement) ? NormalizeCodec(acodecElement.GetString()) : null;
            if (!string.IsNullOrEmpty(acodec) && string.Equals(acodec, "none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (entry.TryGetProperty("vcodec", out var vcodecElement))
            {
                var vcodec = NormalizeCodec(vcodecElement.GetString());
                if (!string.IsNullOrEmpty(vcodec) && !string.Equals(vcodec, "none", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            string? ext = null;
            if (entry.TryGetProperty("ext", out var extElement))
            {
                ext = NormalizeExtension(extElement.GetString());
            }

            if (string.IsNullOrEmpty(ext) && entry.TryGetProperty("container", out var containerElement))
            {
                ext = NormalizeExtension(containerElement.GetString());
            }

            if (string.IsNullOrEmpty(ext))
            {
                ext = ExtractExtensionFromUrl(url);
            }

            return new YouTubeStreamCandidate(url, ext, acodec);
        }

        private static string? NormalizeExtension(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                return null;
            }

            var sanitized = ext.Trim().ToLowerInvariant();
            foreach (var ch in new[] { ';', ',', '+', '/' })
            {
                var idx = sanitized.IndexOf(ch);
                if (idx >= 0)
                {
                    sanitized = sanitized[..idx];
                }
            }

            if (sanitized.StartsWith('.'))
            {
                sanitized = sanitized[1..];
            }

            return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
        }

        private static string? NormalizeCodec(string? codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
            {
                return null;
            }

            return codec.Trim().ToLowerInvariant();
        }

        private static bool IsPreferred(string? ext, string? codec)
        {
            if (!string.IsNullOrEmpty(ext) && PreferredStreamExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(codec) && PreferredStreamCodecs.Any(c => codec.Contains(c, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static bool IsUnsupported(string? ext, string? codec)
        {
            if (!string.IsNullOrEmpty(ext) && UnsupportedStreamExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(codec) && UnsupportedStreamCodecs.Any(c => codec.Contains(c, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static bool IsAcceptable(string? ext, string? codec)
        {
            if (IsUnsupported(ext, codec))
            {
                return false;
            }

            return IsPlayable(ext, codec);
        }

        private static bool IsPlayable(string? ext, string? codec)
        {
            if (!string.IsNullOrEmpty(ext) && CompatibleStreamExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(codec))
            {
                foreach (var known in CompatibleStreamCodecs)
                {
                    if (codec.Contains(known, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string? TryDownloadCompatibleAudio(string link, string? videoId, Action<double>? progressCallback = null)
        {
            try
            {
                Directory.CreateDirectory(YouTubeCacheDirectory);
                var template = Path.Combine(YouTubeCacheDirectory, "%(id)s.%(ext)s");

                var command = BuildYtDlpDownloadCommand(link, template);

                if (!TryRunYtDlp(command, out _, out _, progressCallback))
                {
                    return null;
                }

                var files = Directory.EnumerateFiles(YouTubeCacheDirectory);
                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    files = files.Where(f => Path.GetFileName(f).StartsWith(videoId + ".", StringComparison.OrdinalIgnoreCase));
                }

                var compatible = files
                    .Select(path => new { path, ext = NormalizeExtension(Path.GetExtension(path)) })
                    .Where(entry => IsPlayable(entry.ext, null))
                    .OrderByDescending(entry => System.IO.File.GetLastWriteTimeUtc(entry.path))
                    .FirstOrDefault();

                if (compatible != null)
                {
                    return compatible.path;
                }

                LogDebug("No compatible files found after yt-dlp extract.");
                return null;
            }
            catch (Exception ex)
            {
                LogDebug($"TryDownloadCompatibleAudio exception: {ex.Message}");
                return null;
            }
        }

        private sealed class YouTubeStreamCandidate
        {
            public YouTubeStreamCandidate(string url, string? extension, string? codec)
            {
                Url = url;
                Extension = extension;
                Codec = codec;
            }

            public string Url { get; }
            public string? Extension { get; }
            public string? Codec { get; }
        }

        private static string? TryExtractStreamUrl(JsonElement root)
        {
            if (root.TryGetProperty("requested_downloads", out var requested) && requested.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in requested.EnumerateArray())
                {
                    if (entry.TryGetProperty("url", out var urlElement))
                    {
                        var url = urlElement.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            return url;
                        }
                    }
                }
            }

            if (root.TryGetProperty("url", out var directUrlElement))
            {
                var url = directUrlElement.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }

            if (root.TryGetProperty("formats", out var formatsElement) && formatsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var format in formatsElement.EnumerateArray().Reverse())
                {
                    if (format.TryGetProperty("acodec", out var acodecElement) && acodecElement.GetString() == "none")
                    {
                        continue;
                    }

                    if (format.TryGetProperty("url", out var urlElement))
                    {
                        var url = urlElement.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            return url;
                        }
                    }
                }
            }

            return null;
        }

        private static string? ExtractYouTubeVideoId(string link)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return null;
            }

            var regex = new Regex(@"(?:v=|youtu\.be/|shorts/)([A-Za-z0-9_-]{6,})", RegexOptions.IgnoreCase);
            var match = regex.Match(link);
            return match.Success ? match.Groups[1].Value : null;
        }

        private class DeckState
        {
            public WaveOutEvent? WaveOut;
            public WaveStream? Stream;
            public VolumeSampleProvider? VolumeProvider;
            public ObservableCollection<PlaylistItem> Queue { get; } = new();
            public int CurrentIndex = -1;
            public double Volume = 0.6;
            public int DeviceNumber = -1;
            public double? PendingSeekSeconds;
            public string? LoadedFilePath;
            public bool IsPlaying;
            public double ResumePosition;
            public bool UseVlc;
            public LibVLCSharp.Shared.MediaPlayer? VlcPlayer;
            public Media? VlcMedia;
            public Dictionary<string, string> AppliedHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);
            public string? StreamUrl;
            public EventHandler<EventArgs>? VlcEndReachedHandler;
            public EventHandler<EventArgs>? VlcErrorHandler;
            public bool IsVolumeAutomationUpdate;
            public CancellationTokenSource? FadeCancellation;
        }

        private class DeviceItem
        {
            public int Index { get; }
            public string Name { get; }
            public DeviceItem(int index, string name)
            {
                Index = index;
                Name = name;
            }
        }

        private sealed class YtDlpCommand
        {
            private readonly IReadOnlyList<CommandVariant> _variants;

            public YtDlpCommand(string link, IReadOnlyList<CommandVariant> variants, bool usesCookieFile)
            {
                if (variants == null || variants.Count == 0)
                {
                    throw new ArgumentException("At least one yt-dlp command variant is required.", nameof(variants));
                }

                Link = link;
                _variants = variants;
                UsesCookieFile = usesCookieFile;
            }

            public string Link { get; }
            public bool UsesCookieFile { get; }
            public bool HasFallback => _variants.Count > 1;

            public string BuildArgsForVariant(int index)
            {
                if (index < 0 || index >= _variants.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                var builder = new StringBuilder(_variants[index].Arguments ?? string.Empty);
                builder.Append(" -- \"").Append(EscapeArgument(Link)).Append('"');
                return builder.ToString();
            }

            public bool TryAdvanceVariant(ref int index, out string arguments, out string? description)
            {
                arguments = string.Empty;
                description = null;

                if (index + 1 >= _variants.Count)
                {
                    return false;
                }

                index++;
                arguments = BuildArgsForVariant(index);
                description = _variants[index].Description;
                return true;
            }
        }

        private sealed class CommandVariant
        {
            public CommandVariant(string arguments, string? description)
            {
                Arguments = arguments ?? string.Empty;
                Description = description;
            }

            public string Arguments { get; }
            public string? Description { get; }
        }
    }
}
