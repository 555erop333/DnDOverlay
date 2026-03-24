using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DnDOverlay.Infrastructure;
using DnDOverlay.MiniApps;

namespace DnDOverlay
{
    public class SkillRowViewModel : INotifyPropertyChanged
    {
        private readonly Dictionary<string, int> _playerModifiers;

        public SkillRowViewModel(string name)
        {
            Name = name;
            _playerModifiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; }

        public Dictionary<string, int> Modifiers => _playerModifiers;

        public int this[string playerName]
        {
            get => _playerModifiers.TryGetValue(playerName, out var value) ? value : 0;
            set
            {
                if (_playerModifiers.TryGetValue(playerName, out var existing) && existing == value)
                    return;

                _playerModifiers[playerName] = value;
                RaiseModifierChanged(playerName);
            }
        }

        public void EnsurePlayer(string playerName)
        {
            if (_playerModifiers.ContainsKey(playerName))
                return;

            _playerModifiers[playerName] = 0;
            RaiseModifierChanged(playerName);
        }

        public void RemovePlayer(string playerName)
        {
            if (_playerModifiers.Remove(playerName))
            {
                RaiseModifierChanged(playerName);
            }
        }

        public void RenamePlayer(string oldName, string newName)
        {
            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return;

            if (_playerModifiers.TryGetValue(oldName, out var value))
            {
                _playerModifiers.Remove(oldName);
                _playerModifiers[newName] = value;
                RaiseModifierChanged(oldName);
                RaiseModifierChanged(newName);
            }
            else
            {
                EnsurePlayer(newName);
            }
        }

        public Dictionary<string, int> ExportModifiers()
        {
            return new Dictionary<string, int>(_playerModifiers, StringComparer.OrdinalIgnoreCase);
        }

        public void ImportModifiers(IDictionary<string, int>? modifiers)
        {
            _playerModifiers.Clear();

            if (modifiers != null)
            {
                foreach (var pair in modifiers)
                {
                    _playerModifiers[pair.Key] = pair.Value;
                }
            }

            OnPropertyChanged(string.Empty);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RaiseModifierChanged(string playerName)
        {
            OnPropertyChanged(nameof(Modifiers));
            OnPropertyChanged($"Modifiers[{playerName}]");
            OnPropertyChanged("Item[]");
        }
    }

    public partial class StatsMiniAppWindow : Window, IMiniAppWindow
    {
        private const string WindowKey = "StatsMiniAppWindow";

        private static readonly string[] DefaultSkills =
        {
            "Акробатика", "Анализ", "Атлетика", "Внимание", "Выживание", "Выступление",
            "Запугивание", "История", "Ловкость рук", "Магия", "Медицина", "Обман",
            "Природа", "Проницательность", "Религия", "Скрытность", "Убеждение", "Хитрость"
        };

        private const double RollHistoryRowHeight = 18d;

        private readonly ObservableCollection<string> _playerNames = new();
        private readonly ObservableCollection<SkillRowViewModel> _skills = new();
        private readonly ObservableCollection<RollHistoryEntry> _rollHistory = new();
        private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());
        private readonly ReadOnlyObservableCollection<string> _readOnlyPlayerNames;
        private readonly ReadOnlyObservableCollection<SkillRowViewModel> _readOnlySkills;

        private bool _isLoading;
        private PlayerManagementWindow? _playerManagementWindow;

        public StatsMiniAppWindow()
        {
            InitializeComponent();

            UiScaleManager.RegisterWindow(this);

            DataContext = this;

            _readOnlyPlayerNames = new ReadOnlyObservableCollection<string>(_playerNames);
            _readOnlySkills = new ReadOnlyObservableCollection<SkillRowViewModel>(_skills);

            _playerNames.CollectionChanged += PlayerNamesOnCollectionChanged;
            _skills.CollectionChanged += SkillsOnCollectionChanged;

            // Регистрируем окно для автоматического сохранения/загрузки настроек
            WindowSettingsManager.RegisterWindow(this, WindowKey);

            LoadStatsData();
            InitializeDataGridColumns();
            UpdateRollHistoryHeight();

            Closed += StatsMiniAppWindow_Closed;
        }

        public ObservableCollection<SkillRowViewModel> Skills => _skills;
        public ObservableCollection<RollHistoryEntry> RollHistory => _rollHistory;
        public ReadOnlyObservableCollection<string> PlayerNames => _readOnlyPlayerNames;
        public ReadOnlyObservableCollection<SkillRowViewModel> SkillRows => _readOnlySkills;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // Включаем изменение размера окна
            WindowResizeHelper.EnableResize(this);

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

        public bool AddPlayer(string playerName)
        {
            playerName = playerName.Trim();
            if (string.IsNullOrEmpty(playerName))
                return false;

            if (_playerNames.Any(p => string.Equals(p, playerName, StringComparison.OrdinalIgnoreCase)))
                return false;

            _playerNames.Add(playerName);
            return true;
        }

        public bool TryAddSkill(string skillName)
        {
            skillName = skillName.Trim();
            if (string.IsNullOrEmpty(skillName))
                return false;

            if (_skills.Any(s => string.Equals(s.Name, skillName, StringComparison.OrdinalIgnoreCase)))
                return false;

            AddSkillInternal(skillName, true);
            return true;
        }

        public bool TryRemoveSkill(string skillName)
        {
            var skill = _skills.FirstOrDefault(s => string.Equals(s.Name, skillName, StringComparison.OrdinalIgnoreCase));
            if (skill == null)
                return false;

            _skills.Remove(skill);
            skill.PropertyChanged -= SkillOnPropertyChanged;
            SaveStatsData();
            return true;
        }

        public bool RemovePlayer(string playerName)
        {
            var existing = _playerNames.FirstOrDefault(p => string.Equals(p, playerName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
                return false;

            _playerNames.Remove(existing);
            return true;
        }

        public bool RenamePlayer(string oldName, string newName)
        {
            newName = newName.Trim();
            if (string.IsNullOrEmpty(newName))
                return false;

            var existing = _playerNames.FirstOrDefault(p => string.Equals(p, oldName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
                return false;

            if (_playerNames.Any(p => string.Equals(p, newName, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.Equals(existing, newName, StringComparison.Ordinal))
                    return false;
            }

            var index = _playerNames.IndexOf(existing);
            _playerNames[index] = newName;

            foreach (var skill in _skills)
            {
                skill.RenamePlayer(existing, newName);
            }

            SaveStatsData();
            InitializeDataGridColumns();
            return true;
        }

        public int GetModifier(string playerName, string skillName)
        {
            var skill = _skills.FirstOrDefault(s => string.Equals(s.Name, skillName, StringComparison.OrdinalIgnoreCase));
            return skill?[playerName] ?? 0;
        }

        public void SetModifier(string playerName, string skillName, int modifier)
        {
            var skill = _skills.FirstOrDefault(s => string.Equals(s.Name, skillName, StringComparison.OrdinalIgnoreCase));
            if (skill == null)
                return;

            skill[playerName] = modifier;
            SaveStatsData();
            RefreshSkillsGrid();
        }

        private void StatsMiniAppWindow_Closed(object? sender, EventArgs e)
        {
            _playerManagementWindow?.Close();
            _playerManagementWindow = null;
        }

        private void PlayerNamesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (string player in e.NewItems)
                {
                    foreach (var skill in _skills)
                    {
                        skill.EnsurePlayer(player);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (string player in e.OldItems)
                {
                    foreach (var skill in _skills)
                    {
                        skill.RemovePlayer(player);
                    }
                }
            }

            InitializeDataGridColumns();
            if (!_isLoading)
            {
                SaveStatsData();
            }

            UpdateRollHistoryHeight();
        }

        private void SkillsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (SkillRowViewModel skill in e.NewItems)
                {
                    foreach (var player in _playerNames)
                    {
                        skill.EnsurePlayer(player);
                    }

                    skill.PropertyChanged += SkillOnPropertyChanged;
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (SkillRowViewModel skill in e.OldItems)
                {
                    skill.PropertyChanged -= SkillOnPropertyChanged;
                }
            }

            if (!_isLoading)
            {
                SaveStatsData();
            }
        }

        private void SkillOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading)
                return;

            SaveStatsData();
        }

        private void LoadStatsData()
        {
            _isLoading = true;
            try
            {
                var data = StatsDataManager.LoadStatsData();
                if (data == null)
                {
                    foreach (var skillName in DefaultSkills)
                    {
                        AddSkillInternal(skillName, false);
                    }

                    return;
                }

                _playerNames.Clear();
                if (data.Players != null)
                {
                    foreach (var player in data.Players)
                    {
                        if (!string.IsNullOrWhiteSpace(player.Name))
                        {
                            _playerNames.Add(player.Name);
                        }
                    }
                }

                _skills.Clear();
                var skillNames = data.Skills != null && data.Skills.Count > 0 ? data.Skills : DefaultSkills.ToList();
                foreach (var skillName in skillNames)
                {
                    AddSkillInternal(skillName, false);
                }

                if (data.Players != null)
                {
                    foreach (var player in data.Players)
                    {
                        foreach (var skill in _skills)
                        {
                            if (player.Modifiers != null && player.Modifiers.TryGetValue(skill.Name, out var modifier))
                            {
                                skill[player.Name] = modifier;
                            }
                            else
                            {
                                skill.EnsurePlayer(player.Name);
                            }
                        }
                    }
                }
            }
            finally
            {
                _isLoading = false;
                SaveStatsData();
            }
        }

        private void SaveStatsData()
        {
            if (_isLoading)
                return;

            var data = new StatsDataManager.StatsData
            {
                Skills = _skills.Select(s => s.Name).ToList(),
                Players = _playerNames.Select(name => new StatsDataManager.PlayerData
                {
                    Name = name,
                    Modifiers = _skills.ToDictionary(skill => skill.Name, skill => skill[name])
                }).ToList()
            };

            StatsDataManager.SaveStatsData(data);
        }

        private void RefreshSkillsGrid()
        {
            if (SkillsGrid == null)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RefreshSkillsGrid);
                return;
            }

            SkillsGrid.Items.Refresh();
            ApplyColumnSizing();
        }

        private void InitializeDataGridColumns()
        {
            if (SkillsGrid == null)
                return;

            SkillsGrid.Columns.Clear();

            var nameColumn = new DataGridTextColumn
            {
                Header = "Навык",
                Binding = new Binding(nameof(SkillRowViewModel.Name)),
                IsReadOnly = true,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            SkillsGrid.Columns.Add(nameColumn);

            foreach (var player in _playerNames)
            {
                SkillsGrid.Columns.Add(CreatePlayerColumn(player));
            }

            SkillsGrid.Columns.Add(CreateGroupColumn());

            ApplyColumnSizing();
        }

        private DataGridTemplateColumn CreatePlayerColumn(string playerName)
        {
            var templateColumn = new DataGridTemplateColumn
            {
                Header = GetPlayerHeader(playerName),
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader),
                MinWidth = 0
            };

            var buttonFactory = new FrameworkElementFactory(typeof(Button));
            buttonFactory.SetValue(HeightProperty, 18.0);
            buttonFactory.SetValue(Button.PaddingProperty, new Thickness(1, 0, 1, 0));
            buttonFactory.SetValue(Button.FontSizeProperty, 9.0);
            buttonFactory.SetValue(Button.MarginProperty, new Thickness(1, 0, 0, 0));
            buttonFactory.SetValue(Button.TagProperty, playerName);
            buttonFactory.SetValue(Button.FocusableProperty, false);
            buttonFactory.SetValue(Button.ToolTipProperty, $"Бросить проверку для {playerName}");
            buttonFactory.SetValue(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Center);
            buttonFactory.SetValue(Button.MinWidthProperty, 20.0);
            buttonFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(PlayerSkillButton_OnClick));

            var contentBinding = new Binding($"Modifiers[{playerName}]")
            {
                Mode = BindingMode.OneWay,
                StringFormat = "+0;-0;0"
            };
            buttonFactory.SetBinding(ContentControl.ContentProperty, contentBinding);

            var cellTemplate = new DataTemplate { VisualTree = buttonFactory };
            templateColumn.CellTemplate = cellTemplate;

            return templateColumn;
        }

        private static string GetPlayerHeader(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return string.Empty;

            return playerName.Length <= 3
                ? playerName
                : playerName.Substring(0, 3);
        }

        private void ApplyColumnSizing()
        {
            if (SkillsGrid == null)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ApplyColumnSizing);
                return;
            }

            SkillsGrid.UpdateLayout();

            for (var i = 1; i < SkillsGrid.Columns.Count; i++)
            {
                var column = SkillsGrid.Columns[i];

                if (Equals(column.Header, "Группа"))
                {
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader);
                }
                else
                {
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
                }
            }

            SkillsGrid.UpdateLayout();

            for (var i = 1; i < SkillsGrid.Columns.Count; i++)
            {
                var column = SkillsGrid.Columns[i];
                column.MinWidth = column.ActualWidth;
            }
        }

        private DataGridTemplateColumn CreateGroupColumn()
        {
            var templateColumn = new DataGridTemplateColumn
            {
                Header = "Группа",
                Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader),
                MinWidth = 0
            };

            var buttonFactory = new FrameworkElementFactory(typeof(Button));
            buttonFactory.SetValue(HeightProperty, 18.0);
            buttonFactory.SetValue(Button.PaddingProperty, new Thickness(2, 0, 2, 0));
            buttonFactory.SetValue(Button.FontSizeProperty, 9.0);
            buttonFactory.SetValue(Button.MarginProperty, new Thickness(1, 0, 0, 0));
            buttonFactory.SetValue(Button.ContentProperty, "Все");
            buttonFactory.SetValue(Button.FocusableProperty, false);
            buttonFactory.SetValue(Button.ToolTipProperty, "Групповая проверка");
            buttonFactory.SetValue(Button.MinWidthProperty, 32.0);
            buttonFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(GroupSkillButton_OnClick));

            var cellTemplate = new DataTemplate { VisualTree = buttonFactory };
            templateColumn.CellTemplate = cellTemplate;

            return templateColumn;
        }

        private void PlayerSkillButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not SkillRowViewModel skill)
                return;

            if (button.Tag is not string playerName)
                return;

            ExecuteSkillRoll(skill, playerName);
        }

        private void GroupSkillButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not SkillRowViewModel skill)
                return;

            PerformGroupRoll(skill);
        }

        private void PerformGroupRoll(SkillRowViewModel skill)
        {
            if (_playerNames.Count == 0)
                return;

            foreach (var player in _playerNames)
            {
                var entry = BuildRollEntry(player, skill[player]);
                AppendHistory(entry);
            }
        }

        private void ExecuteSkillRoll(SkillRowViewModel skill, string playerName)
        {
            var entry = BuildRollEntry(playerName, skill[playerName]);
            AppendHistory(entry);
        }

        private RollHistoryEntry BuildRollEntry(string playerName, int modifier)
        {
            var d20 = _random.Next(1, 21);
            var total = d20 + modifier;

            return new RollHistoryEntry
            {
                PlayerName = playerName,
                Roll = d20,
                Modifier = modifier,
                Total = total,
                IsCriticalSuccess = d20 == 20,
                IsCriticalFailure = d20 == 1
            };
        }

        private void AppendHistory(RollHistoryEntry entry)
        {
            foreach (var existing in _rollHistory)
            {
                existing.IsLatest = false;
            }

            entry.IsLatest = true;
            _rollHistory.Insert(0, entry);

            const int maxEntries = 100;
            if (_rollHistory.Count > maxEntries)
            {
                _rollHistory.RemoveAt(_rollHistory.Count - 1);
            }

            RollHistoryList.ScrollIntoView(entry);
        }

        private static string FormatModifier(int modifier) => modifier >= 0 ? $"+{modifier}" : modifier.ToString();

        private void UpdateRollHistoryHeight()
        {
            if (RollHistoryList == null)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateRollHistoryHeight);
                return;
            }

            var visibleRows = Math.Max(1, _playerNames.Count + 1);
            RollHistoryList.Height = visibleRows * RollHistoryRowHeight;
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

        private void ManagePlayers_OnClick(object sender, RoutedEventArgs e)
        {
            if (_playerManagementWindow == null || !_playerManagementWindow.IsLoaded)
            {
                _playerManagementWindow = new PlayerManagementWindow(this)
                {
                    Owner = this
                };
                _playerManagementWindow.Show();
            }
            else
            {
                _playerManagementWindow.Activate();
            }
        }

        private void ClearHistory_OnClick(object sender, RoutedEventArgs e)
        {
            _rollHistory.Clear();
        }

        private void AddSkillInternal(string skillName, bool saveAfterAdd)
        {
            var skill = new SkillRowViewModel(skillName);
            foreach (var player in _playerNames)
            {
                skill.EnsurePlayer(player);
            }

            skill.PropertyChanged += SkillOnPropertyChanged;
            _skills.Add(skill);

            if (saveAfterAdd)
            {
                SaveStatsData();
            }
        }

        public class RollHistoryEntry : INotifyPropertyChanged
        {
            private bool _isLatest;

            public string PlayerName { get; set; } = string.Empty;
            public int Roll { get; set; }
            public int Modifier { get; set; }
            public int Total { get; set; }
            public bool IsCriticalSuccess { get; set; }
            public bool IsCriticalFailure { get; set; }
            public string ModifierDisplay => FormatModifier(Modifier);

            public bool IsLatest
            {
                get => _isLatest;
                set
                {
                    if (_isLatest == value)
                    {
                        return;
                    }

                    _isLatest = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
