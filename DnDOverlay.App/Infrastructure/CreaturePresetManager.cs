using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnDOverlay.Infrastructure
{
    /// <summary>
    /// Класс для управления сохранением и загрузкой пресетов существ
    /// </summary>
    public static class CreaturePresetManager
    {
        private static readonly string PresetsFilePath =
            AppPaths.GetDataFilePath("creature_presets.json");

        /// <summary>
        /// Данные пресета существа
        /// </summary>
        public class CreaturePreset
        {
            public string Name { get; set; } = string.Empty;
            public int InitiativeModifier { get; set; }
            public int Hp { get; set; }
            public int? ArmorClass { get; set; }
        }

        /// <summary>
        /// Контейнер для всех пресетов
        /// </summary>
        public class PresetsData
        {
            public List<CreaturePreset> Presets { get; set; } = new List<CreaturePreset>();
        }

        /// <summary>
        /// Сохраняет пресеты в файл
        /// </summary>
        public static void SavePresets(List<CreaturePreset> presets)
        {
            try
            {
                // Создаем директорию, если её нет
                var data = new PresetsData { Presets = presets };
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never
                };

                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(PresetsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения пресетов: {ex.Message}");
            }
        }

        /// <summary>
        /// Загружает пресеты из файла
        /// </summary>
        public static List<CreaturePreset> LoadPresets()
        {
            try
            {
                if (File.Exists(PresetsFilePath))
                {
                    var json = File.ReadAllText(PresetsFilePath);
                    var data = JsonSerializer.Deserialize<PresetsData>(json);
                    return data?.Presets ?? new List<CreaturePreset>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки пресетов: {ex.Message}");
            }

            return new List<CreaturePreset>();
        }

        /// <summary>
        /// Добавляет новый пресет
        /// </summary>
        public static void AddPreset(CreaturePreset preset)
        {
            var presets = LoadPresets();
            
            // Проверяем, не существует ли уже пресет с таким именем
            var existing = presets.FirstOrDefault(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                // Обновляем существующий
                existing.InitiativeModifier = preset.InitiativeModifier;
                existing.Hp = preset.Hp;
                existing.ArmorClass = preset.ArmorClass;
            }
            else
            {
                presets.Add(preset);
            }
            
            SavePresets(presets);
        }

        /// <summary>
        /// Удаляет пресет по имени
        /// </summary>
        public static void DeletePreset(string name)
        {
            var presets = LoadPresets();
            presets.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            SavePresets(presets);
        }
    }
}
