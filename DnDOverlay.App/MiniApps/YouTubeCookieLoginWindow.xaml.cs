using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace DnDOverlay
{
    public partial class YouTubeCookieLoginWindow : Window
    {
        private static readonly string[] CookieHosts =
        {
            "https://www.youtube.com",
            "https://music.youtube.com",
            "https://studio.youtube.com",
            "https://accounts.google.com",
            "https://myaccount.google.com",
            "https://www.google.com"
        };

        private static readonly string[] RequiredCookieNames =
        {
            "SAPISID",
            "__Secure-3PAPISID"
        };

        private readonly string _cookieFilePath;
        private bool _webViewInitialized;

        public YouTubeCookieLoginWindow(string cookieFilePath)
        {
            InitializeComponent();
            _cookieFilePath = cookieFilePath ?? throw new ArgumentNullException(nameof(cookieFilePath));
            Loaded += YouTubeCookieLoginWindow_Loaded;
        }

        private async void YouTubeCookieLoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            if (_webViewInitialized)
            {
                return;
            }

            try
            {
                StatusTextBlock.Text = "Инициализация WebView2...";
                var environment = await CoreWebView2Environment.CreateAsync();
                await YouTubeWebView.EnsureCoreWebView2Async(environment);
                _webViewInitialized = true;

                YouTubeWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                YouTubeWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                YouTubeWebView.CoreWebView2.Settings.IsZoomControlEnabled = true;

                NavigateToYouTube();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Не удалось запустить WebView2: {ex.Message}";
            }
        }

        private void NavigateToYouTube()
        {
            if (!_webViewInitialized || YouTubeWebView.CoreWebView2 == null)
            {
                return;
            }

            const string url = "https://www.youtube.com";
            StatusTextBlock.Text = "Откройте YouTube и выполните вход.";
            YouTubeWebView.CoreWebView2.Navigate(url);
        }

        private void OpenYouTubeButton_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateToYouTube();
        }

        private async void SaveCookiesButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_webViewInitialized || YouTubeWebView.CoreWebView2 == null)
            {
                StatusTextBlock.Text = "WebView2 не инициализирован.";
                return;
            }

            try
            {
                StatusTextBlock.Text = "Получаем cookies...";
                var cookies = await CollectCookiesAsync();

                if (cookies.Count == 0)
                {
                    StatusTextBlock.Text = "Куки не найдены. Убедитесь, что вы выполнили вход.";
                    return;
                }

                if (!ContainsRequiredAuthCookies(cookies, out var missingList))
                {
                    StatusTextBlock.Text =
                        "Не хватает авторизационных куки (" + missingList + "). Перейдите на accounts.google.com в окне ниже, войдите и попробуйте снова.";
                    return;
                }

                var lines = SerializeCookies(cookies);
                var directory = Path.GetDirectoryName(_cookieFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await using var stream = new FileStream(_cookieFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                foreach (var line in lines)
                {
                    await writer.WriteLineAsync(line);
                }

                StatusTextBlock.Text = $"Сохранено {cookies.Count} cookies.";
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Не удалось сохранить cookies: {ex.Message}";
            }
        }

        private async Task<IReadOnlyList<CoreWebView2Cookie>> CollectCookiesAsync()
        {
            var manager = YouTubeWebView.CoreWebView2.CookieManager;

            var map = new Dictionary<string, CoreWebView2Cookie>(StringComparer.Ordinal);

            foreach (var host in CookieHosts)
            {
                try
                {
                    var hostCookies = await manager.GetCookiesAsync(host);
                    foreach (var cookie in hostCookies)
                    {
                        if (cookie == null || string.IsNullOrEmpty(cookie.Name))
                        {
                            continue;
                        }

                        var key = string.Join('|', cookie.Domain ?? string.Empty, cookie.Path ?? string.Empty, cookie.Name);
                        map[key] = cookie;
                    }
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Не удалось получить cookies для {host}: {ex.Message}";
                }
            }

            return map.Values.ToList();
        }

        private static IEnumerable<string> SerializeCookies(IReadOnlyList<CoreWebView2Cookie> cookies)
        {
            var result = new List<string>
            {
                "# Netscape HTTP Cookie File",
                "# This file was generated by DnDOverlay",
                string.Empty
            };

            foreach (var cookie in cookies.Where(c => !string.IsNullOrEmpty(c.Name)))
            {
                var domain = cookie.Domain ?? string.Empty;
                if (string.IsNullOrWhiteSpace(domain))
                {
                    continue;
                }

                bool includeSubdomainFlag = domain.StartsWith(".", StringComparison.Ordinal);
                var includeSubdomains = includeSubdomainFlag ? "TRUE" : "FALSE";

                var outputDomain = cookie.IsHttpOnly ? "#HttpOnly_" + domain : domain;

                var path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;
                var secure = cookie.IsSecure ? "TRUE" : "FALSE";
                long expires = 0;
                try
                {
                    if (!cookie.IsSession)
                    {
                        var expiry = cookie.Expires;
                        expires = new DateTimeOffset(expiry.ToUniversalTime()).ToUnixTimeSeconds();
                    }
                }
                catch
                {
                    expires = 0;
                }

                var value = cookie.Value ?? string.Empty;
                var line = string.Join('\t', outputDomain, includeSubdomains, path, secure, expires.ToString(), cookie.Name, value);
                result.Add(line);
            }

            return result;
        }

        private static bool ContainsRequiredAuthCookies(IReadOnlyCollection<CoreWebView2Cookie> cookies, out string missingList)
        {
            var names = new HashSet<string>(cookies.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            var missing = RequiredCookieNames.Where(name => !names.Contains(name)).ToList();
            missingList = missing.Count == 0 ? string.Empty : string.Join(", ", missing);
            return missing.Count == 0;
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void DeleteCookiesButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_webViewInitialized && YouTubeWebView.CoreWebView2 != null)
                {
                    var manager = YouTubeWebView.CoreWebView2.CookieManager;
                    manager.DeleteAllCookies();
                }

                if (!string.IsNullOrWhiteSpace(_cookieFilePath) && File.Exists(_cookieFilePath))
                {
                    File.Delete(_cookieFilePath);
                }

                StatusTextBlock.Text = "Локальные куки удалены. Выполните вход заново и сохраните их ещё раз.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Не удалось удалить куки: {ex.Message}";
            }

            // Reload YouTube so the user can sign in again
            await Dispatcher.InvokeAsync(NavigateToYouTube);
        }
    }
}
