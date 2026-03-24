using System;
using System.IO;

namespace DnDOverlay.Infrastructure
{
    internal static class AppPaths
    {
        public static string BaseDirectory { get; } =
            AppContext.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            ?? Environment.CurrentDirectory;

        public static string AudioDirectory { get; } = Path.Combine(BaseDirectory, "Sounds");
        public static string MusicDirectory { get; } = Path.Combine(BaseDirectory, "Music");
        public static string BackgroundDirectory { get; } = Path.Combine(BaseDirectory, "Background");
        public static string DataDirectory { get; } = Path.Combine(BaseDirectory, "Data");

        static AppPaths()
        {
            EnsureBaseDirectories();
        }

        public static string LogsDirectory => GetDataSubdirectory("Logs");

        public static string ToolsDirectory => GetDataSubdirectory("Tools");

        public static string WebViewUserDataDirectory => GetDataSubdirectory("WebView2");

        public static void EnsureBaseDirectories()
        {
            TryCreateDirectory(AudioDirectory);
            TryCreateDirectory(MusicDirectory);
            TryCreateDirectory(BackgroundDirectory);
            TryCreateDirectory(DataDirectory);
        }

        public static string GetDataFilePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            var fullPath = Path.Combine(DataDirectory, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                TryCreateDirectory(directory);
            }
            return fullPath;
        }

        public static string GetDataSubdirectory(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            var directory = Path.Combine(DataDirectory, relativePath);
            TryCreateDirectory(directory);
            return directory;
        }

        private static void TryCreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch
            {
                // Ignore directory creation failures here; specific callers will handle errors if needed.
            }
        }
    }
}
