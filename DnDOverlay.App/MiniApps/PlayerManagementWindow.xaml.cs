using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DnDOverlay.Infrastructure;

namespace DnDOverlay
{
    public partial class PlayerManagementWindow : Window, INotifyPropertyChanged
    {
        private const string WindowKey = "PlayerManagementWindow";

        private readonly StatsMiniAppWindow _statsWindow;
        private readonly ObservableCollection<SkillModifierViewModel> _skillModifiers = new();
        private readonly HashSet<SkillRowViewModel> _subscribedSkills = new();
        private readonly INotifyCollectionChanged _playerNamesNotifier;
        private readonly INotifyCollectionChanged _skillRowsNotifier;

        private string? _selectedPlayerName;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PlayerManagementWindow(StatsMiniAppWindow statsWindow)
        {
            _statsWindow = statsWindow ?? throw new ArgumentNullException(nameof(statsWindow));

            InitializeComponent();
            DataContext = this;

            WindowResizeHelper.EnableResize(this);
            WindowSettingsManager.RegisterWindow(this, WindowKey);
            TopmostToggleBinder.Bind(this, MiniAppTopmostToggle);

            _playerNamesNotifier = (INotifyCollectionChanged)_statsWindow.PlayerNames;
            _skillRowsNotifier = (INotifyCollectionChanged)_statsWindow.SkillRows;

            _playerNamesNotifier.CollectionChanged += PlayerNamesOnCollectionChanged;
            _skillRowsNotifier.CollectionChanged += SkillRowsOnCollectionChanged;
            foreach (var skill in _statsWindow.SkillRows)
            {
                SubscribeSkill(skill);
            }

            Closed += PlayerManagementWindow_OnClosed;
            Loaded += PlayerManagementWindow_OnLoaded;
        }

        public ReadOnlyObservableCollection<string> PlayerNames => _statsWindow.PlayerNames;

        public ReadOnlyObservableCollection<SkillRowViewModel> SkillRows => _statsWindow.SkillRows;

        public ObservableCollection<SkillModifierViewModel> SkillModifiers => _skillModifiers;

        public bool HasSelectedPlayer => !string.IsNullOrEmpty(SelectedPlayerName);

        public string SelectedPlayerLabel => HasSelectedPlayer
            ? $"Модификаторы навыков: {SelectedPlayerName}"
            : "Модификаторы навыков: —";

        public string? SelectedPlayerName
        {
            get => _selectedPlayerName;
            private set
            {
                if (string.Equals(_selectedPlayerName, value, StringComparison.Ordinal))
                    return;

                _selectedPlayerName = value;

                if (RenamePlayerTextBox != null)
                {
                    RenamePlayerTextBox.Text = value ?? string.Empty;
                }

                OnPropertyChanged(nameof(SelectedPlayerName));
                OnPropertyChanged(nameof(HasSelectedPlayer));
                OnPropertyChanged(nameof(SelectedPlayerLabel));

                RefreshSkillModifiers();
            }
        }

        private void PlayerManagementWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedPlayer && PlayerNames.Count > 0)
            {
                SelectedPlayerName = PlayerNames[0];
                PlayersList.SelectedItem = SelectedPlayerName;
            }

            RefreshSkillModifiers();
        }

        private void AddPlayer_OnClick(object sender, RoutedEventArgs e)
        {
            var name = AddPlayerTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите имя игрока.", "Игроки", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_statsWindow.AddPlayer(name))
            {
                MessageBox.Show("Игрок с таким именем уже существует.", "Игроки", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AddPlayerTextBox.Clear();
            PlayersList.SelectedItem = name;
            SelectedPlayerName = name;
        }

        private void RenamePlayer_OnClick(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedPlayer)
                return;

            var newName = RenamePlayerTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Введите новое имя игрока.", "Игроки", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var currentName = SelectedPlayerName!;
            if (!_statsWindow.RenamePlayer(currentName, newName))
            {
                MessageBox.Show("Не удалось переименовать игрока. Проверьте, что имя уникально.", "Игроки", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PlayersList.SelectedItem = newName;
            SelectedPlayerName = newName;
        }

        private void RemovePlayer_OnClick(object sender, RoutedEventArgs e)
        {
            if (!HasSelectedPlayer)
                return;

            var player = SelectedPlayerName!;
            if (MessageBox.Show($"Удалить игрока '{player}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (_statsWindow.RemovePlayer(player))
            {
                SelectedPlayerName = null;
                PlayersList.SelectedItem = null;
            }
        }

        private void PlayersList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlayersList.SelectedItem is string playerName)
            {
                SelectedPlayerName = playerName;
            }
            else if (e.RemovedItems != null && e.RemovedItems.Count > 0 && SelectedPlayerName != null)
            {
                SelectedPlayerName = null;
            }
        }

        private void AddSkill_OnClick(object sender, RoutedEventArgs e)
        {
            var skillName = AddSkillTextBox.Text.Trim();
            if (string.IsNullOrEmpty(skillName))
            {
                MessageBox.Show("Введите название навыка.", "Навыки", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_statsWindow.TryAddSkill(skillName))
            {
                MessageBox.Show("Такой навык уже существует.", "Навыки", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AddSkillTextBox.Clear();
        }

        private void RemoveSkill_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            var skillName = button.Tag as string;
            if (string.IsNullOrEmpty(skillName))
            {
                return;
            }

            if (MessageBox.Show($"Удалить навык '{skillName}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _statsWindow.TryRemoveSkill(skillName);
        }

        private void PlayerNamesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (string removed in e.OldItems)
                {
                    if (string.Equals(removed, SelectedPlayerName, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedPlayerName = null;
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace && e.OldItems != null && e.NewItems != null)
            {
                var oldName = e.OldItems[0] as string;
                var newName = e.NewItems[0] as string;
                if (!string.IsNullOrEmpty(oldName) && !string.IsNullOrEmpty(newName) &&
                    string.Equals(oldName, SelectedPlayerName, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedPlayerName = newName;
                    PlayersList.SelectedItem = newName;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add && SelectedPlayerName == null && PlayerNames.Count > 0)
            {
                SelectedPlayerName = PlayerNames[0];
                PlayersList.SelectedItem = SelectedPlayerName;
            }
        }

        private void SkillRowsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SkillRowViewModel skill in e.OldItems)
                {
                    UnsubscribeSkill(skill);
                }
            }

            if (e.NewItems != null)
            {
                foreach (SkillRowViewModel skill in e.NewItems)
                {
                    SubscribeSkill(skill);
                }
            }

            RefreshSkillModifiers();
        }

        private void SkillRowOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!HasSelectedPlayer || sender is not SkillRowViewModel skill)
                return;

            if (string.IsNullOrEmpty(e.PropertyName) ||
                string.Equals(e.PropertyName, $"Modifiers[{SelectedPlayerName}]", StringComparison.Ordinal))
            {
                var viewModel = _skillModifiers.FirstOrDefault(x => x.SkillName == skill.Name);
                if (viewModel != null)
                {
                    viewModel.SetModifierSilently(skill[SelectedPlayerName!]);
                }
            }
        }

        private void RefreshSkillModifiers()
        {
            _skillModifiers.Clear();

            if (!HasSelectedPlayer)
                return;

            var playerName = SelectedPlayerName!;
            foreach (var skill in _statsWindow.SkillRows)
            {
                var modifier = new SkillModifierViewModel(skill.Name, playerName, ModifierOnModifierChanged);
                modifier.SetModifierSilently(skill[playerName]);
                _skillModifiers.Add(modifier);
            }
        }

        private void ModifierOnModifierChanged(string playerName, string skillName, int value)
        {
            if (!string.Equals(playerName, SelectedPlayerName, StringComparison.Ordinal))
                return;

            _statsWindow.SetModifier(playerName, skillName, value);
        }

        private void ModifierTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            e.Handled = !IsModifierInputValid(textBox.Text, textBox.SelectionStart, textBox.SelectionLength, e.Text);
        }

        private void ModifierTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (textBox.DataContext is not SkillModifierViewModel viewModel)
                return;

            if (TryParseModifier(textBox.Text, out var value) && viewModel.Modifier != value)
            {
                viewModel.Modifier = value;
            }
        }

        private void ModifierTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!IsModifierInputValid(textBox.Text, textBox.SelectionStart, textBox.SelectionLength, pastedText))
            {
                e.CancelCommand();
            }
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

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PlayerManagementWindow_OnClosed(object? sender, EventArgs e)
        {
            _playerNamesNotifier.CollectionChanged -= PlayerNamesOnCollectionChanged;
            _skillRowsNotifier.CollectionChanged -= SkillRowsOnCollectionChanged;
            foreach (var skill in _subscribedSkills)
            {
                skill.PropertyChanged -= SkillRowOnPropertyChanged;
            }
            _subscribedSkills.Clear();
        }

        private void SubscribeSkill(SkillRowViewModel skill)
        {
            if (_subscribedSkills.Add(skill))
            {
                skill.PropertyChanged += SkillRowOnPropertyChanged;
            }
        }

        private void UnsubscribeSkill(SkillRowViewModel skill)
        {
            if (_subscribedSkills.Remove(skill))
            {
                skill.PropertyChanged -= SkillRowOnPropertyChanged;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static bool TryParseModifier(string text, out int value)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                value = 0;
                return false;
            }

            return int.TryParse(text, out value);
        }

        private static bool IsModifierInputValid(string currentText, int selectionStart, int selectionLength, string newText)
        {
            var updated = BuildUpdatedText(currentText ?? string.Empty, selectionStart, selectionLength, newText);
            if (updated.Length == 0)
                return true;

            if (updated == "-")
                return true;

            return int.TryParse(updated, out _);
        }

        private static string BuildUpdatedText(string currentText, int selectionStart, int selectionLength, string newText)
        {
            selectionStart = Math.Clamp(selectionStart, 0, currentText.Length);
            selectionLength = Math.Clamp(selectionLength, 0, currentText.Length - selectionStart);
            return currentText.Remove(selectionStart, selectionLength)
                               .Insert(selectionStart, newText ?? string.Empty);
        }

        public class SkillModifierViewModel : INotifyPropertyChanged
        {
            private readonly string _playerName;
            private readonly string _skillName;
            private readonly Action<string, string, int> _modifierChangedCallback;
            private int _modifier;
            private bool _suppressNotification;

            public SkillModifierViewModel(string skillName, string playerName, Action<string, string, int> modifierChangedCallback)
            {
                _skillName = skillName;
                _playerName = playerName;
                _modifierChangedCallback = modifierChangedCallback;
            }

            public string SkillName => _skillName;

            public int Modifier
            {
                get => _modifier;
                set
                {
                    if (_modifier == value)
                        return;

                    _modifier = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Modifier)));

                    if (!_suppressNotification)
                    {
                        _modifierChangedCallback(_playerName, _skillName, value);
                    }
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public void SetModifierSilently(int value)
            {
                _suppressNotification = true;
                try
                {
                    Modifier = value;
                }
                finally
                {
                    _suppressNotification = false;
                }
            }
        }
    }
}
