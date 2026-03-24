using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DnDOverlay.Infrastructure;
using DnDOverlay.MiniApps;

namespace DnDOverlay
{
    public partial class DiceMiniAppWindow : Window, IMiniAppWindow
    {
        private const string WindowKey = "DiceMiniAppWindow";
        private readonly Random _random = new();
        private readonly ObservableCollection<string> _history = new();
        private readonly ObservableCollection<DiceEntry> _diceEntries = new();
        private int _modifier;
        private DiceHistoryWindow? _historyWindow;

        public DiceMiniAppWindow()
        {
            InitializeComponent();
            
            UiScaleManager.RegisterWindow(this);
            
            // Регистрируем окно для автоматического сохранения/загрузки настроек
            WindowSettingsManager.RegisterWindow(this, WindowKey);
            
            // Привязываем переключатель к состоянию Topmost
            TopmostToggleBinder.Bind(this, MiniAppTopmostToggle);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            
            // Включаем изменение размера окна
            WindowResizeHelper.EnableResize(this);
            
            InitializeDiceEntries();
            DiceEntriesList.ItemsSource = _diceEntries;
            UpdateModifierText();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_historyWindow != null)
            {
                _historyWindow.Closed -= HistoryWindow_OnClosed;
                _historyWindow.Close();
                _historyWindow = null;
            }

            HistoryToggleButton.Content = "История";
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

        private void Window_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null)
            {
                return;
            }

            var buttonAncestor = FindAncestor<ButtonBase>(originalSource);
            if (buttonAncestor != null)
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

        private void Roll_OnClick(object sender, RoutedEventArgs e)
        {
            var total = 0;
            var breakdownParts = new List<string>();
            var hasDice = false;

            foreach (var entry in _diceEntries)
            {
                if (entry.Count <= 0)
                {
                    continue;
                }

                hasDice = true;
                var rolls = RollDice(entry.Count, entry.Sides);
                var sum = 0;
                foreach (var value in rolls)
                {
                    sum += value;
                }

                total += sum;
                breakdownParts.Add($"{entry.Count}d{entry.Sides}: {sum} ({string.Join(", ", rolls)})");
            }

            var modifierValue = _modifier;
            total += modifierValue;

            if (!hasDice && modifierValue == 0)
            {
                ResultText.Text = "";
                return;
            }

            var resultString = modifierValue == 0
                ? total.ToString()
                : $"{total} ({(modifierValue >= 0 ? "+" : string.Empty)}{modifierValue})";

            ResultText.Text = resultString;

            if (modifierValue != 0)
            {
                breakdownParts.Add($"Мод: {(modifierValue >= 0 ? "+" : string.Empty)}{modifierValue}");
            }

            var historyEntry = $"Сумма {total}: {string.Join("; ", breakdownParts)}";
            if (_history.Count == 0 || _history[0] != historyEntry)
            {
                _history.Insert(0, historyEntry);
            }

            TrimHistory();
            UpdateHistoryWindow();
        }

        private IList<int> RollDice(int count, int sides)
        {
            var rolls = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                rolls.Add(_random.Next(1, sides + 1));
            }

            return rolls;
        }

        private void TrimHistory()
        {
            if (_history.Count > 20)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }

        private void UpdateHistoryWindow()
        {
            if (_historyWindow == null)
            {
                return;
            }

            _historyWindow.RefreshView();
        }

        private void InitializeDiceEntries()
        {
            _diceEntries.Clear();

            var dicePresets = new (string Name, int Sides)[]
            {
                ("D100", 100),
                ("D20", 20),
                ("D12", 12),
                ("D10", 10),
                ("D8", 8),
                ("D6", 6),
                ("D4", 4),
                ("D2", 2)
            };

            foreach (var preset in dicePresets)
            {
                _diceEntries.Add(new DiceEntry(preset.Name, preset.Sides));
            }
        }

        private void DiceMinus_OnClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DiceEntry entry)
            {
                entry.Count--;
            }
        }

        private void DicePlus_OnClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DiceEntry entry)
            {
                entry.Count++;
            }
        }

        private void DiceCountTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDigitsOnly(e.Text);
        }

        private void DiceCountTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (!IsDigitsOnly(text))
            {
                e.CancelCommand();
            }
        }

        private void DiceCountTextBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DiceEntry entry)
            {
                entry.Count = 0;
                e.Handled = true;
            }
        }

        private void TextBox_SelectAll_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private void TextBox_SelectAll_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            if (!textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void ModifierInput_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SetModifier(0);
            e.Handled = true;
        }

        private void ModifierMinus_OnClick(object sender, RoutedEventArgs e)
        {
            ChangeModifier(-1);
        }

        private void ModifierPlus_OnClick(object sender, RoutedEventArgs e)
        {
            ChangeModifier(1);
        }

        private void ModifierInput_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                e.Handled = true;
                return;
            }

            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;
            var currentText = textBox.Text ?? string.Empty;
            var prospectiveText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, e.Text);

            e.Handled = !IsValidModifierText(prospectiveText);
        }

        private void ModifierInput_OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (!IsValidModifierText(text ?? string.Empty))
            {
                e.CancelCommand();
            }
        }

        private void ModifierInput_OnLostFocus(object sender, RoutedEventArgs e)
        {
            var text = ModifierInput.Text;
            if (string.IsNullOrWhiteSpace(text) || text == "-")
            {
                SetModifier(0);
                return;
            }

            if (!int.TryParse(text, out var value))
            {
                UpdateModifierText();
                return;
            }

            SetModifier(value);
        }

        private void HistoryToggleButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_historyWindow == null || !_historyWindow.IsLoaded)
            {
                _historyWindow = new DiceHistoryWindow();
                _historyWindow.Owner = this;
                _historyWindow.SetHistorySource(_history);
                _historyWindow.Closed += HistoryWindow_OnClosed;
                _historyWindow.Show();
                HistoryToggleButton.Content = "Скрыть";
            }
            else
            {
                _historyWindow.Close();
            }
        }

        private void HistoryWindow_OnClosed(object? sender, EventArgs e)
        {
            if (_historyWindow != null)
            {
                _historyWindow.Closed -= HistoryWindow_OnClosed;
                _historyWindow = null;
            }

            HistoryToggleButton.Content = "История";
        }

        private void ChangeModifier(int delta)
        {
            SetModifier(_modifier + delta);
        }

        private void SetModifier(int value)
        {
            if (_modifier == value)
            {
                UpdateModifierText();
                return;
            }

            _modifier = value;
            UpdateModifierText();
        }

        private void UpdateModifierText()
        {
            ModifierInput.Text = _modifier.ToString();
        }

        private static bool IsDigitsOnly(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var ch in text)
            {
                if (!char.IsDigit(ch))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidModifierText(string text)
        {
            if (string.IsNullOrEmpty(text) || text == "-")
            {
                return true;
            }

            return int.TryParse(text, out _);
        }

        private class DiceEntry : INotifyPropertyChanged
        {
            private int _count;

            public DiceEntry(string name, int sides)
            {
                Name = name;
                Sides = sides;
            }

            public string Name { get; }

            public int Sides { get; }

            public int Count
            {
                get => _count;
                set
                {
                    var newValue = Math.Max(0, value);
                    if (newValue == _count)
                    {
                        return;
                    }

                    _count = newValue;
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(nameof(CountText));
                }
            }

            public string CountText
            {
                get => _count.ToString();
                set
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        if (_count != 0)
                        {
                            _count = 0;
                            OnPropertyChanged(nameof(Count));
                        }

                        return;
                    }

                    if (int.TryParse(value, out var parsed))
                    {
                        Count = parsed;
                    }
                    else
                    {
                        OnPropertyChanged(nameof(CountText));
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
