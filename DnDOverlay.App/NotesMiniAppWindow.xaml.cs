using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DnDOverlay.Infrastructure;
using DnDOverlay.MiniApps;

namespace DnDOverlay
{
    public partial class NotesMiniAppWindow : Window, IMiniAppWindow
    {
        private const string WindowKey = "NotesMiniAppWindow";
        private bool _isLoadingNotes;
        private static readonly char[] LinkBoundaryChars = { '"', '\'', '<', '>', '(', ')', '[', ']', '{', '}', '|', '\\' };
        private static readonly char[] LinkTrimChars = { '.', ',', ';', ':', ')', ']', '}', '>', '"', '\'' };
        
        public NotesMiniAppWindow()
        {
            InitializeComponent();
            
            UiScaleManager.RegisterWindow(this);

            // Регистрируем окно для автоматического сохранения/загрузки настроек
            WindowSettingsManager.RegisterWindow(this, WindowKey);

            LoadSavedNotes();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            
            // Включаем изменение размера окна и отключаем системное снаппинг/масштабирование
            WindowResizeHelper.EnableResize(this, disableSystemResize: true);
            
            // Привязываем переключатель к состоянию Topmost
            TopmostToggleBinder.Bind(this, MiniAppTopmostToggle);
        }

        public void SetTopmost(bool isTopmost)
        {
            Topmost = isTopmost;
            
            // Сохраняем настройку закрепленности
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
            CloseWindow();
        }

        private void Clear_OnClick(object sender, RoutedEventArgs e)
        {
            NotesTextBox.Clear();
        }

        private void NotesTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isLoadingNotes)
            {
                return;
            }

            SaveNotes();
        }

        private void NotesTextBox_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                return;
            }

            if (sender is not TextBox textBox)
            {
                return;
            }

            var clickPosition = e.GetPosition(textBox);
            var charIndex = textBox.GetCharacterIndexFromPoint(clickPosition, true);
            if (charIndex < 0)
            {
                return;
            }

            var link = ExtractLinkAtIndex(textBox.Text, charIndex);
            if (link == null)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Не удалось открыть ссылку '{link}': {ex.Message}");
            }
        }

        private void LoadSavedNotes()
        {
            _isLoadingNotes = true;

            var savedNotes = NotesDataManager.LoadNotes();
            if (savedNotes != null)
            {
                NotesTextBox.Text = savedNotes;
            }

            _isLoadingNotes = false;
        }

        private void SaveNotes()
        {
            NotesDataManager.SaveNotes(NotesTextBox.Text);
        }

        private static string? ExtractLinkAtIndex(string text, int index)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            if (index >= text.Length)
            {
                index = text.Length - 1;
            }

            if (index < 0)
            {
                return null;
            }

            if (IsLinkBoundary(text[index]) && index > 0)
            {
                index--;
            }

            if (index < 0 || IsLinkBoundary(text[index]))
            {
                return null;
            }

            var start = index;
            while (start > 0 && !IsLinkBoundary(text[start - 1]))
            {
                start--;
            }

            var end = index;
            while (end < text.Length - 1 && !IsLinkBoundary(text[end + 1]))
            {
                end++;
            }

            var candidate = text.Substring(start, end - start + 1);
            candidate = candidate.Trim(LinkTrimChars);

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                return null;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return candidate;
        }

        private static bool IsLinkBoundary(char c)
        {
            return char.IsWhiteSpace(c) || char.IsControl(c) || Array.IndexOf(LinkBoundaryChars, c) >= 0;
        }
    }
}
