using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DnDOverlay.Infrastructure;

namespace DnDOverlay
{
    /// <summary>
    /// Модель для отображения пресета в списке
    /// </summary>
    public class PresetViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _initiativeModifier;
        private int _hp;
        private int? _armorClass;

        public string Name
        {
            get => _name;
            set
            {
                var newValue = (value ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(newValue))
                {
                    OnPropertyChanged(nameof(Name));
                    return;
                }

                if (_name == newValue)
                    return;

                _name = newValue;
                OnPropertyChanged();
            }
        }

        public int InitiativeModifier
        {
            get => _initiativeModifier;
            set
            {
                var clamped = Math.Max(-30, Math.Min(30, value));
                if (_initiativeModifier == clamped)
                    return;

                _initiativeModifier = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InitiativeModifierDisplay));
            }
        }

        public string InitiativeModifierDisplay => InitiativeModifier >= 0 ? $"+{InitiativeModifier}" : InitiativeModifier.ToString();

        public int Hp
        {
            get => _hp;
            set
            {
                var sanitized = Math.Max(1, value);
                if (_hp == sanitized)
                    return;

                _hp = sanitized;
                OnPropertyChanged();
            }
        }

        public int? ArmorClass
        {
            get => _armorClass;
            set
            {
                int? sanitized = value;
                if (value.HasValue)
                {
                    var clamped = Math.Max(1, value.Value);
                    sanitized = clamped;
                }

                if (_armorClass == sanitized)
                    return;

                _armorClass = sanitized;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class CreaturePresetsWindow : Window
    {
        private ObservableCollection<PresetViewModel> _presets = new();
        public event EventHandler? PresetsChanged;

        public CreaturePresetsWindow()
        {
            InitializeComponent();
            PresetsList.ItemsSource = _presets;
            LoadPresets();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            WindowResizeHelper.EnableResize(this);
        }

        private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddPreset_OnClick(object sender, RoutedEventArgs e)
        {
            var name = PresetNameInput.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите имя существа", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PresetInitModInput.Text, out int initMod))
                initMod = 0;

            if (!int.TryParse(PresetHpInput.Text, out int hp))
            {
                MessageBox.Show("Введите корректное значение ХП", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int? armorClass = null;
            var acText = PresetCdInput.Text.Trim();
            if (!string.IsNullOrEmpty(acText))
            {
                if (int.TryParse(acText, out int acValue) && acValue > 0)
                {
                    armorClass = acValue;
                }
                else
                {
                    MessageBox.Show("Введите корректное значение КД", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var preset = new CreaturePresetManager.CreaturePreset
            {
                Name = name,
                InitiativeModifier = initMod,
                Hp = hp,
                ArmorClass = armorClass
            };

            CreaturePresetManager.AddPreset(preset);
            LoadPresets();

            // Очищаем поля
            PresetNameInput.Clear();
            PresetInitModInput.Text = "0";
            PresetHpInput.Clear();
            PresetCdInput.Clear();
            PresetNameInput.Focus();

            // Уведомляем об изменении пресетов
            PresetsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DeletePreset_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var preset = button?.Tag as PresetViewModel;
            if (preset == null) return;

            var result = MessageBox.Show(
                $"Удалить пресет \"{preset.Name}\"?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                CreaturePresetManager.DeletePreset(preset.Name);
                LoadPresets();

                // Уведомляем об изменении пресетов
                PresetsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void LoadPresets()
        {
            foreach (var preset in _presets)
            {
                preset.PropertyChanged -= Preset_PropertyChanged;
            }

            _presets.Clear();
            var presets = CreaturePresetManager.LoadPresets();
            foreach (var preset in presets)
            {
                var viewModel = new PresetViewModel
                {
                    Name = preset.Name,
                    InitiativeModifier = preset.InitiativeModifier,
                    Hp = preset.Hp,
                    ArmorClass = preset.ArmorClass
                };

                viewModel.PropertyChanged += Preset_PropertyChanged;
                _presets.Add(viewModel);
            }
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

        private void PresetInitModTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not PresetViewModel preset)
                return;

            if (!int.TryParse(textBox.Text, out var value))
            {
                textBox.Text = preset.InitiativeModifier.ToString();
                return;
            }

            var clamped = Math.Max(-30, Math.Min(30, value));
            if (clamped != value)
            {
                textBox.Text = clamped.ToString();
            }

            preset.InitiativeModifier = clamped;
        }

        private void PresetHpTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not PresetViewModel preset)
                return;

            if (!int.TryParse(textBox.Text, out var value) || value <= 0)
            {
                value = Math.Max(1, preset.Hp);
            }

            if (value != preset.Hp)
            {
                preset.Hp = value;
            }

            textBox.Text = preset.Hp.ToString();
        }

        private void PresetAcTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not PresetViewModel preset)
                return;

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                preset.ArmorClass = null;
                textBox.Text = string.Empty;
                return;
            }

            if (!int.TryParse(textBox.Text, out var value) || value <= 0)
            {
                value = preset.ArmorClass ?? 10;
            }

            if (value != preset.ArmorClass)
            {
                preset.ArmorClass = value;
            }

            textBox.Text = preset.ArmorClass?.ToString() ?? string.Empty;
        }

        private void Preset_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SaveAllPresets();
            PresetsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SaveAllPresets()
        {
            var presets = _presets
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => new CreaturePresetManager.CreaturePreset
                {
                    Name = p.Name,
                    InitiativeModifier = p.InitiativeModifier,
                    Hp = p.Hp,
                    ArmorClass = p.ArmorClass
                })
                .ToList();

            CreaturePresetManager.SavePresets(presets);
        }

        private void PresetInitModUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PresetInitModInput.Text, out int value))
            {
                value = Math.Min(30, value + 1);
                PresetInitModInput.Text = value.ToString();
            }
        }

        private void PresetInitModDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PresetInitModInput.Text, out int value))
            {
                value = Math.Max(-30, value - 1);
                PresetInitModInput.Text = value.ToString();
            }
        }
    }
}
