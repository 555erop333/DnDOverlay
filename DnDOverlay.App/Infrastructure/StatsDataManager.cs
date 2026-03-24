using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DnDOverlay.Infrastructure
{
    public static class StatsDataManager
    {
        private static readonly string StatsDataFilePath =
            AppPaths.GetDataFilePath("stats_data.json");

        public class PlayerData
        {
            public string Name { get; set; } = string.Empty;
            public Dictionary<string, int> Modifiers { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public class StatsData
        {
            public List<string> Skills { get; set; } = new List<string>();
            public List<PlayerData> Players { get; set; } = new List<PlayerData>();
        }

        public static void SaveStatsData(StatsData data)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(StatsDataFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения данных статов: {ex.Message}");
            }
        }

        public static StatsData? LoadStatsData()
        {
            try
            {
                if (File.Exists(StatsDataFilePath))
                {
                    var json = File.ReadAllText(StatsDataFilePath);
                    return JsonSerializer.Deserialize<StatsData>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки данных статов: {ex.Message}");
            }

            return null;
        }
    }
}
