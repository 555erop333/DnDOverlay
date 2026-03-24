using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DnDOverlay.Infrastructure
{
    public static class MusicDataManager
    {
        private static readonly string MusicMetaFilePath =
            AppPaths.GetDataFilePath("music_metadata.json");

        public class TrackMetadata
        {
            public string? CustomTitle { get; set; }
            public string? Description { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
        }

        public class MusicMetadata
        {
            public Dictionary<string, TrackMetadata> Tracks { get; set; } = new Dictionary<string, TrackMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        public static MusicMetadata LoadAll()
        {
            try
            {
                if (File.Exists(MusicMetaFilePath))
                {
                    var json = File.ReadAllText(MusicMetaFilePath);
                    return JsonSerializer.Deserialize<MusicMetadata>(json) ?? new MusicMetadata();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки метаданных музыки: {ex.Message}");
            }

            return new MusicMetadata();
        }

        public static void SaveAll(MusicMetadata data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MusicMetaFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения метаданных музыки: {ex.Message}");
            }
        }

        public static TrackMetadata GetOrCreate(MusicMetadata data, string key)
        {
            if (!data.Tracks.TryGetValue(key, out var meta))
            {
                meta = new TrackMetadata();
                data.Tracks[key] = meta;
            }
            return meta;
        }
    }
}
