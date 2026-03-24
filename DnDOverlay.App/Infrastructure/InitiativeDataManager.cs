using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnDOverlay.Infrastructure
{
    /// <summary>
    /// Класс для управления сохранением и загрузкой данных инициативы
    /// </summary>
    public static class InitiativeDataManager
    {
        private static readonly string InitiativeDataFilePath =
            AppPaths.GetDataFilePath("initiative_data.json");

        /// <summary>
        /// Данные одного существа для сохранения
        /// </summary>
        public class CreatureData
        {
            public string Name { get; set; } = string.Empty;
            public int CreatureNumber { get; set; }
            public int Initiative { get; set; }
            public int InitiativeModifier { get; set; }
            public int CurrentHp { get; set; }
            public int MaxHp { get; set; }
            public int TempHp { get; set; }
            public bool HasActed { get; set; }
            public bool IsDead { get; set; }
            public int? ArmorClass { get; set; }
            public string? GroupColorHex { get; set; }
        }

        /// <summary>
        /// Запись журнала боя
        /// </summary>
        public class BattleLogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        /// <summary>
        /// Все данные инициативы
        /// </summary>
        public class InitiativeData
        {
            public List<CreatureData> Creatures { get; set; } = new List<CreatureData>();
            public int CurrentRound { get; set; } = 1;
            public Dictionary<string, int> CreatureCounters { get; set; } = new Dictionary<string, int>();
            public List<BattleLogEntry> BattleLog { get; set; } = new List<BattleLogEntry>();
        }

        /// <summary>
        /// Сохраняет данные инициативы в файл
        /// </summary>
        public static void SaveInitiativeData(InitiativeData data)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never
                };

                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(InitiativeDataFilePath, json);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем работу приложения
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения данных инициативы: {ex.Message}");
            }
        }

        /// <summary>
        /// Загружает данные инициативы из файла
        /// </summary>
        public static InitiativeData? LoadInitiativeData()
        {
            try
            {
                if (File.Exists(InitiativeDataFilePath))
                {
                    var json = File.ReadAllText(InitiativeDataFilePath);
                    return JsonSerializer.Deserialize<InitiativeData>(json);
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем работу приложения
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки данных инициативы: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Удаляет сохраненные данные инициативы
        /// </summary>
        public static void ClearInitiativeData()
        {
            try
            {
                if (File.Exists(InitiativeDataFilePath))
                {
                    File.Delete(InitiativeDataFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления данных инициативы: {ex.Message}");
            }
        }
    }
}
