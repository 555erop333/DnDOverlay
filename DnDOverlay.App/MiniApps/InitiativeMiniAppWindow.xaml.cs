using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DnDOverlay.Infrastructure;
using DnDOverlay.MiniApps;
using BattleLogEntry = DnDOverlay.Infrastructure.InitiativeDataManager.BattleLogEntry;

namespace DnDOverlay
{
    /// <summary>
    /// Класс для отображения пресета в ComboBox
    /// </summary>
    public class PresetComboBoxItem
    {
        public string Name { get; set; } = string.Empty;
        public int InitiativeModifier { get; set; }
        public int Hp { get; set; }
        public int? ArmorClass { get; set; }
    }

    public class CombatCreature : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _initiative;
        private int _initiativeModifier;
        private int _currentHp;
        private int _maxHp;
        private int _tempHp;
        private bool _hasActed;
        private bool _isDead;
        private int _creatureNumber;
        private int? _armorClass;
        private string? _groupColorHex;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public int CreatureNumber
        {
            get => _creatureNumber;
            set { _creatureNumber = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string DisplayName => CreatureNumber > 0 ? $"{Name} ({CreatureNumber})" : Name;

        public int Initiative
        {
            get => _initiative;
            set { _initiative = value; OnPropertyChanged(); }
        }

        public int InitiativeModifier
        {
            get => _initiativeModifier;
            set { _initiativeModifier = value; OnPropertyChanged(); OnPropertyChanged(nameof(InitiativeModifierDisplay)); }
        }

        public string InitiativeModifierDisplay => InitiativeModifier >= 0 ? $"+{InitiativeModifier}" : InitiativeModifier.ToString();

        public int CurrentHp
        {
            get => _currentHp;
            set 
            { 
                _currentHp = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(HpDisplay));
                OnPropertyChanged(nameof(HealthStatus));
            }
        }

        public int MaxHp
        {
            get => _maxHp;
            set 
            { 
                _maxHp = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(HpDisplay));
                OnPropertyChanged(nameof(HealthStatus));
            }
        }

        public int TempHp
        {
            get => _tempHp;
            set { _tempHp = value; OnPropertyChanged(); OnPropertyChanged(nameof(HpDisplay)); }
        }

        public string HpDisplay => TempHp > 0 ? $"{CurrentHp}/{MaxHp}+{TempHp}" : $"{CurrentHp}/{MaxHp}";

        public int? ArmorClass
        {
            get => _armorClass;
            set { _armorClass = value; OnPropertyChanged(); OnPropertyChanged(nameof(ArmorClassDisplay)); }
        }

        public string ArmorClassDisplay => ArmorClass?.ToString() ?? string.Empty;

        public string? GroupColorHex
        {
            get => _groupColorHex;
            set
            {
                if (_groupColorHex == value)
                    return;
                _groupColorHex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GroupColorBrush));
            }
        }

        public Brush? GroupColorBrush => TryCreateBrush(GroupColorHex);

        public string HealthStatus
        {
            get
            {
                if (MaxHp <= 0)
                {
                    return "Danger";
                }

                double percentage = (double)CurrentHp / MaxHp;

                if (percentage >= 0.6)
                {
                    return "Good";
                }

                if (percentage >= 0.3)
                {
                    return "Warning";
                }

                return "Danger";
            }
        }

        public bool HasActed
        {
            get => _hasActed;
            set { _hasActed = value; OnPropertyChanged(); }
        }

        public bool IsDead
        {
            get => _isDead;
            set { _isDead = value; OnPropertyChanged(); }
        }

        private static Brush? TryCreateBrush(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return null;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class InitiativeMiniAppWindow : Window, IMiniAppWindow
    {
        private const string WindowKey = "InitiativeMiniAppWindow";
        private ObservableCollection<CombatCreature> _creatures = new();
        private ObservableCollection<BattleLogEntry> _battleLog = new();
        private int _currentRound = 1;
        private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());
        private Dictionary<string, int> _creatureCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private CreaturePresetsWindow? _presetsWindow = null;
        private BattleLogWindow? _battleLogWindow = null;

        public InitiativeMiniAppWindow()
        {
            InitializeComponent();
            UiScaleManager.RegisterWindow(this);
            CombatGrid.ItemsSource = _creatures;
            UpdateRoundCounter();
            
            // Регистрируем окно для автоматического сохранения/загрузки настроек
            WindowSettingsManager.RegisterWindow(this, WindowKey);
            
            // Загружаем данные инициативы
            LoadInitiativeData();
            
            // Загружаем пресеты в ComboBox
            LoadPresetsToComboBox();
            
            // Подписываемся на изменения коллекции
            _creatures.CollectionChanged += (s, e) => SaveInitiativeData();
            _battleLog.CollectionChanged += (s, e) => SaveInitiativeData();
            
            // Привязываем переключатель к состоянию Topmost
            TopmostToggleBinder.Bind(this, MiniAppTopmostToggle);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            
            // Включаем изменение размера окна
            WindowResizeHelper.EnableResize(this);
        }

        public void SetTopmost(bool isTopmost)
        {
            Topmost = isTopmost;
            
            // Сохраняем настройку закрепленности
            WindowSettingsManager.UpdateTopmostSetting(this, WindowKey, isTopmost);

            if (_battleLogWindow != null && _battleLogWindow.IsLoaded)
            {
                _battleLogWindow.SetTopmost(isTopmost);
            }
        }

        public void ShowWindow()
        {
            Show();
        }

        public void CloseWindow()
        {
            Close();
        }

        private void BattleLog_Click(object sender, RoutedEventArgs e)
        {
            if (_battleLogWindow != null && _battleLogWindow.IsLoaded)
            {
                _battleLogWindow.Activate();
                return;
            }

            _battleLogWindow = new BattleLogWindow
            {
                Owner = this
            };

            _battleLogWindow.SetLogSource(_battleLog);
            _battleLogWindow.SetTopmost(Topmost);
            _battleLogWindow.ClearLogRequested += BattleLogWindow_ClearLogRequested;
            _battleLogWindow.Closed += (_, _) =>
            {
                if (_battleLogWindow != null)
                {
                    _battleLogWindow.ClearLogRequested -= BattleLogWindow_ClearLogRequested;
                    _battleLogWindow = null;
                }
            };

            _battleLogWindow.Show();
        }

        private void BattleLogWindow_ClearLogRequested(object? sender, EventArgs e)
        {
            _battleLog.Clear();
            SaveInitiativeData();
        }

        private void LogEvent(string message, bool removeExisting = false)
        {
            if (removeExisting)
            {
                for (int i = _battleLog.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(_battleLog[i].Message, message, StringComparison.Ordinal))
                    {
                        _battleLog.RemoveAt(i);
                    }
                }
            }

            if (_battleLog.LastOrDefault()?.Message == message)
            {
                return;
            }

            var entry = new BattleLogEntry
            {
                Timestamp = DateTime.Now,
                Message = message
            };

            _battleLog.Add(entry);

            if (_battleLogWindow != null && _battleLogWindow.IsLoaded)
            {
                _battleLogWindow.ScrollToEnd();
            }
        }

        private static string FormatModifier(int value) => value >= 0 ? $"+{value}" : value.ToString();

        private void LogCreaturesAdded(string name, int initMod, int hp, int? armorClass, int count, int initiativeRoll, IReadOnlyCollection<CombatCreature> addedCreatures)
        {
            var armorPart = armorClass.HasValue ? $", КД {armorClass}" : string.Empty;

            if (addedCreatures.Count == 1)
            {
                var creature = addedCreatures.First();
                LogEvent($"{creature.DisplayName}: добавлен (иниц. {creature.Initiative}, мод. {FormatModifier(creature.InitiativeModifier)}, ХП {creature.MaxHp}{armorPart})");
            }
            else if (addedCreatures.Count > 1)
            {
                LogEvent($"{count}× {name}: добавлены (иниц. {initiativeRoll}, мод. {FormatModifier(initMod)}, ХП {hp}{armorPart})");
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
            SetTopmost(isTopmost);
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            CloseWindow();
        }

        private void Add_OnClick(object sender, RoutedEventArgs e)
        {
            AddCreatures();
        }

        private void AddCreatures()
        {
            var name = NameInput.Text.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            if (!int.TryParse(InitModInput.Text, out int initMod))
                initMod = 0;

            if (!int.TryParse(HpInput.Text, out int hp))
                hp = 1;

            int? armorClass = null;
            var acText = AcInput.Text.Trim();
            if (!string.IsNullOrEmpty(acText))
            {
                if (int.TryParse(acText, out int acValue) && acValue > 0)
                {
                    armorClass = acValue;
                }
                else
                {
                    MessageBox.Show("Введите корректное значение КД", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AcInput.Focus();
                    AcInput.SelectAll();
                    return;
                }
            }

            if (!int.TryParse(CountInput.Text, out int count))
                count = 1;

            count = Math.Max(1, Math.Min(30, count));

            // Для группы существ бросаем инициативу ОДИН раз для всех
            int initiativeRoll = _random.Next(1, 21) + initMod;

            var addedCreatures = new List<CombatCreature>();

            bool shouldAssignNumbers = count > 1 || HasCreatureWithName(name);

            for (int i = 0; i < count; i++)
            {
                var creature = new CombatCreature
                {
                    Name = name,
                    Initiative = initiativeRoll, // Все существа группы получают одинаковую инициативу
                    InitiativeModifier = initMod,
                    CurrentHp = hp,
                    MaxHp = hp,
                    TempHp = 0,
                    HasActed = false,
                    IsDead = false,
                    ArmorClass = armorClass
                };

                // Присваиваем номер, если необходимо обеспечить уникальные имена
                if (shouldAssignNumbers)
                {
                    creature.CreatureNumber = GetNextCreatureNumber(name);
                }

                // Подписываемся на изменения свойств перед добавлением
                creature.PropertyChanged += Creature_PropertyChanged;

                _creatures.Add(creature);
                addedCreatures.Add(creature);
            }

            // Сортируем по инициативе
            SortCreaturesByInitiative();

            LogCreaturesAdded(name, initMod, hp, armorClass, count, initiativeRoll, addedCreatures);

            // Очищаем поля
            NameInput.Clear();
            InitModInput.Text = "0";
            HpInput.Clear();
            AcInput.Clear();
            CountInput.Text = "1";
            NameInput.Focus();
        }

        private void SortCreaturesByInitiative()
        {
            // Сортируем: сначала живые по инициативе, потом мертвые в конце
            var sorted = _creatures.OrderBy(c => c.IsDead).ThenByDescending(c => c.Initiative).ToList();
            _creatures.Clear();
            foreach (var creature in sorted)
                _creatures.Add(creature);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9-]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void PositiveNumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        // Спиннеры для модификатора инициативы
        private void InitModUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(InitModInput.Text, out int value))
            {
                value = Math.Min(30, value + 1);
                InitModInput.Text = value.ToString();
            }
        }

        private void InitModDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(InitModInput.Text, out int value))
            {
                value = Math.Max(-30, value - 1);
                InitModInput.Text = value.ToString();
            }
        }

        private void InitModInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(InitModInput.Text, out int value))
            {
                if (value > 30) InitModInput.Text = "30";
                else if (value < -30) InitModInput.Text = "-30";
            }
        }

        // Спиннеры для количества
        private void CountUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(CountInput.Text, out int value))
            {
                value = Math.Min(30, value + 1);
                CountInput.Text = value.ToString();
            }
        }

        private void CountDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(CountInput.Text, out int value))
            {
                value = Math.Max(1, value - 1);
                CountInput.Text = value.ToString();
            }
        }

        private void CountInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(CountInput.Text, out int value))
            {
                if (value > 30) CountInput.Text = "30";
                else if (value < 1) CountInput.Text = "1";
            }
        }

        private void GroupColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not CombatCreature creature)
                return;

            var colorHex = button.Tag as string;

            if (!string.IsNullOrWhiteSpace(colorHex))
            {
                if (string.Equals(creature.GroupColorHex, colorHex, StringComparison.OrdinalIgnoreCase))
                {
                    creature.GroupColorHex = null;
                }
                else
                {
                    creature.GroupColorHex = colorHex;
                }
            }
            else
            {
                creature.GroupColorHex = null;
            }
        }

        // Действия с существами
        private void DamageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not CombatCreature creature)
                return;

            if (button.Parent is not StackPanel stackPanel)
                return;

            var damageInput = stackPanel.Children[0] as TextBox;

            if (damageInput != null && int.TryParse(damageInput.Text, out int damage) && damage > 0)
            {
                int originalDamage = damage;
                int initialTempHp = creature.TempHp;
                int initialHp = creature.CurrentHp;
                bool wasDead = creature.IsDead;

                int remainingDamage = damage;

                if (creature.TempHp > 0)
                {
                    if (remainingDamage <= creature.TempHp)
                    {
                        creature.TempHp -= remainingDamage;
                        remainingDamage = 0;
                    }
                    else
                    {
                        remainingDamage -= creature.TempHp;
                        creature.TempHp = 0;
                    }
                }

                if (remainingDamage > 0)
                {
                    creature.CurrentHp = Math.Max(0, creature.CurrentHp - remainingDamage);
                }

                bool becameDead = creature.CurrentHp == 0 && !creature.IsDead;
                if (becameDead)
                {
                    creature.IsDead = true;
                    MoveCreatureToEnd(creature);
                }

                string tempPart = initialTempHp != creature.TempHp
                    ? $" (врем. ХП {initialTempHp}->{creature.TempHp})"
                    : string.Empty;
                string deathPart = becameDead && !wasDead ? " [мертв]" : string.Empty;

                LogEvent($"{creature.DisplayName}: получил {originalDamage} урона (ХП {initialHp}->{creature.CurrentHp}){tempPart}{deathPart}");

                damageInput.Clear();
            }
        }

        private void HealButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not CombatCreature creature)
                return;

            if (button.Parent is not StackPanel stackPanel)
                return;

            var healInput = stackPanel.Children[0] as TextBox;

            if (healInput != null && int.TryParse(healInput.Text, out int heal) && heal > 0)
            {
                bool wasDeadBefore = creature.IsDead;
                int initialHp = creature.CurrentHp;

                creature.CurrentHp = Math.Min(creature.MaxHp, creature.CurrentHp + heal);

                int actualHeal = creature.CurrentHp - initialHp;
                bool revived = wasDeadBefore && creature.CurrentHp > 0;

                if (revived)
                {
                    creature.IsDead = false;
                    SortCreaturesByInitiative();
                }

                if (actualHeal > 0)
                {
                    string revivePart = revived ? " (воскрешен)" : string.Empty;
                    LogEvent($"{creature.DisplayName}: восстановил {actualHeal} ХП (ХП {initialHp}->{creature.CurrentHp}){revivePart}");
                }
                else if (revived)
                {
                    LogEvent($"{creature.DisplayName}: возвращен к жизни (ХП {creature.CurrentHp}/{creature.MaxHp})");
                }

                healInput.Clear();
            }
        }

        private void TempHpButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not CombatCreature creature)
                return;

            if (button.Parent is not StackPanel stackPanel)
                return;

            var tempHpInput = stackPanel.Children[0] as TextBox;

            if (tempHpInput != null && int.TryParse(tempHpInput.Text, out int tempHp) && tempHp > 0)
            {
                int initialTempHp = creature.TempHp;
                creature.TempHp = Math.Max(creature.TempHp, tempHp);

                if (creature.TempHp != initialTempHp)
                {
                    LogEvent($"{creature.DisplayName}: временные ХП {initialTempHp}->{creature.TempHp}");
                }

                tempHpInput.Clear();
            }
        }

        private void KillButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not CombatCreature creature)
                return;

            if (creature.IsDead)
            {
                RemoveCreature(creature);
                LogEvent($"{creature.DisplayName}: удален из списка");
                return;
            }

            creature.IsDead = true;
            creature.CurrentHp = 0;
            creature.TempHp = 0;
            MoveCreatureToEnd(creature);
            LogEvent($"{creature.DisplayName}: помечен как мертвый");
        }

        private void UpdateRoundCounter()
        {
            RoundCounter.Text = $"Раунд {_currentRound}";
        }

        // Функция для перехода к следующему раунду (можно вызвать когда все существа сделали ход)
        public void NextRound()
        {
            _currentRound++;
            foreach (var creature in _creatures)
            {
                creature.HasActed = false;
            }
            UpdateRoundCounter();
            LogEvent($"Начался раунд {_currentRound}");
            SaveInitiativeData();
        }

        private bool TryAdvanceRoundIfAllActed()
        {
            if (_creatures.Count == 0)
            {
                return false;
            }

            if (_creatures.All(c => c.HasActed))
            {
                NextRound();
                return true;
            }

            return false;
        }

        private void Creature_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            bool advancedRound = false;

            if (sender is CombatCreature creature && e.PropertyName == nameof(CombatCreature.HasActed) && creature.HasActed)
            {
                advancedRound = TryAdvanceRoundIfAllActed();
            }

            if (!advancedRound)
            {
                SaveInitiativeData();
            }
        }

        private void HasActedCheckBox_Click(object sender, RoutedEventArgs e)
        {
            CombatGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CombatGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void NextRound_Click(object sender, RoutedEventArgs e)
        {
            NextRound();
        }

        private void ClearCombat_Click(object sender, RoutedEventArgs e)
        {
            _creatures.Clear();
            _creatureCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _currentRound = 1;
            UpdateRoundCounter();
            _battleLog.Clear();
            LogEvent("Начат новый бой", removeExisting: true);
            
            // Удаляем сохраненные данные
            InitiativeDataManager.ClearInitiativeData();
        }

        private bool HasCreatureWithName(string name) =>
            _creatures.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        private int GetNextCreatureNumber(string name)
        {
            if (!_creatureCounters.TryGetValue(name, out int lastNumber))
            {
                lastNumber = _creatures
                    .Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.CreatureNumber)
                    .DefaultIfEmpty(0)
                    .Max();
            }

            int candidate = lastNumber;

            do
            {
                candidate++;
            } while (_creatures.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) && c.CreatureNumber == candidate));

            _creatureCounters[name] = candidate;
            return candidate;
        }

        private void EnsureCounterAtLeast(string name, int value)
        {
            if (value <= 0)
                return;

            if (_creatureCounters.TryGetValue(name, out int existing))
            {
                if (value > existing)
                {
                    _creatureCounters[name] = value;
                }
            }
            else
            {
                _creatureCounters[name] = value;
            }
        }

        private void MoveCreatureToEnd(CombatCreature creature)
        {
            var index = _creatures.IndexOf(creature);
            if (index >= 0)
            {
                _creatures.RemoveAt(index);
                _creatures.Add(creature);

                EnsureCounterAtLeast(creature.Name, creature.CreatureNumber);
            }
        }

        private void RemoveCreature(CombatCreature creature)
        {
            creature.PropertyChanged -= Creature_PropertyChanged;
            _creatures.Remove(creature);
            SaveInitiativeData();
        }

        /// <summary>
        /// Сохраняет текущие данные инициативы
        /// </summary>
        private void SaveInitiativeData()
        {
            var data = new InitiativeDataManager.InitiativeData
            {
                CurrentRound = _currentRound,
                CreatureCounters = new Dictionary<string, int>(_creatureCounters),
                Creatures = new List<InitiativeDataManager.CreatureData>(),
                BattleLog = _battleLog
                    .Select(entry => new BattleLogEntry
                    {
                        Timestamp = entry.Timestamp,
                        Message = entry.Message
                    })
                    .ToList()
            };

            foreach (var creature in _creatures)
            {
                data.Creatures.Add(new InitiativeDataManager.CreatureData
                {
                    Name = creature.Name,
                    CreatureNumber = creature.CreatureNumber,
                    Initiative = creature.Initiative,
                    InitiativeModifier = creature.InitiativeModifier,
                    CurrentHp = creature.CurrentHp,
                    MaxHp = creature.MaxHp,
                    TempHp = creature.TempHp,
                    HasActed = creature.HasActed,
                    IsDead = creature.IsDead,
                    ArmorClass = creature.ArmorClass,
                    GroupColorHex = creature.GroupColorHex
                });
            }

            InitiativeDataManager.SaveInitiativeData(data);
        }

        /// <summary>
        /// Загружает сохраненные данные инициативы
        /// </summary>
        private void LoadInitiativeData()
        {
            var data = InitiativeDataManager.LoadInitiativeData();
            if (data == null)
                return;

            _currentRound = data.CurrentRound;
            _creatureCounters = data.CreatureCounters != null
                ? new Dictionary<string, int>(data.CreatureCounters, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var creatureData in data.Creatures)
            {
                var creature = new CombatCreature
                {
                    Name = creatureData.Name,
                    CreatureNumber = creatureData.CreatureNumber,
                    Initiative = creatureData.Initiative,
                    InitiativeModifier = creatureData.InitiativeModifier,
                    CurrentHp = creatureData.CurrentHp,
                    MaxHp = creatureData.MaxHp,
                    TempHp = creatureData.TempHp,
                    HasActed = creatureData.HasActed,
                    IsDead = creatureData.IsDead,
                    ArmorClass = creatureData.ArmorClass,
                    GroupColorHex = creatureData.GroupColorHex
                };

                // Подписываемся на изменения свойств каждого существа
                creature.PropertyChanged += Creature_PropertyChanged;

                _creatures.Add(creature);
            }

            _battleLog.Clear();
            if (data.BattleLog != null && data.BattleLog.Count > 0)
            {
                var seenEntries = new HashSet<(DateTime Timestamp, string Message)>();

                foreach (var entry in data.BattleLog.OrderBy(x => x.Timestamp))
                {
                    var key = (entry.Timestamp, entry.Message ?? string.Empty);
                    if (!seenEntries.Add(key))
                    {
                        continue;
                    }

                    _battleLog.Add(entry);
                }
            }

            UpdateRoundCounter();
        }

        /// <summary>
        /// Загружает пресеты в ComboBox
        /// </summary>
        private void LoadPresetsToComboBox()
        {
            var presets = CreaturePresetManager.LoadPresets();
            var items = new List<PresetComboBoxItem>();
            
            // Добавляем пустой элемент в начало
            items.Add(new PresetComboBoxItem { Name = "-- Выбрать пресет --", InitiativeModifier = 0, Hp = 0 });
            
            foreach (var preset in presets)
            {
                items.Add(new PresetComboBoxItem
                {
                    Name = preset.Name,
                    InitiativeModifier = preset.InitiativeModifier,
                    Hp = preset.Hp,
                    ArmorClass = preset.ArmorClass
                });
            }
            
            PresetComboBox.ItemsSource = items;
            PresetComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Пресеты"
        /// </summary>
        private void Presets_Click(object sender, RoutedEventArgs e)
        {
            // Если окно уже открыто, активируем его
            if (_presetsWindow != null && _presetsWindow.IsLoaded)
            {
                _presetsWindow.Activate();
                return;
            }
            
            // Создаем новое окно
            _presetsWindow = new CreaturePresetsWindow
            {
                Owner = this
            };
            
            // Подписываемся на событие изменения пресетов
            _presetsWindow.PresetsChanged += (s, args) => LoadPresetsToComboBox();
            
            // Открываем окно
            _presetsWindow.Show();
        }

        /// <summary>
        /// Обработчик выбора пресета из ComboBox
        /// </summary>
        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is not PresetComboBoxItem preset || preset.Hp <= 0)
            {
                return;
            }

            NameInput.Text = preset.Name;
            InitModInput.Text = preset.InitiativeModifier.ToString();
            HpInput.Text = preset.Hp.ToString();
            AcInput.Text = preset.ArmorClass?.ToString() ?? string.Empty;

            AddCreatures();

            // Возвращаем ComboBox к значению по умолчанию
            PresetComboBox.SelectedIndex = 0;
        }
    }
}
