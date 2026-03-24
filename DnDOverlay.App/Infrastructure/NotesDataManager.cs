using System;
using System.Diagnostics;
using System.IO;

namespace DnDOverlay.Infrastructure
{
    public static class NotesDataManager
    {
        private static readonly string NotesDataFilePath =
            AppPaths.GetDataFilePath("notes.txt");

        public static void SaveNotes(string? text)
        {
            try
            {
                File.WriteAllText(NotesDataFilePath, text ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения заметок: {ex.Message}");
            }
        }

        public static string? LoadNotes()
        {
            try
            {
                if (File.Exists(NotesDataFilePath))
                {
                    return File.ReadAllText(NotesDataFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки заметок: {ex.Message}");
            }

            return null;
        }
    }
}
