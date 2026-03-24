using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnDOverlay.Infrastructure
{
    internal static class ExternalToolInstaller
    {
        private const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const string FfmpegDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        private const string NodeDownloadUrl = "https://nodejs.org/dist/v20.10.0/node-v20.10.0-win-x64.zip";
        private const string YtDlpExecutableName = "yt-dlp.exe";
        private const string FfmpegExecutableName = "ffmpeg.exe";
        private const string NodeExecutableName = "node.exe";
        private const string NodeArchiveExecutablePath = "node-v20.10.0-win-x64/node.exe";
        private const string YtDlpMinimumVersionString = "2024.08.06";
        private static readonly DateTime YtDlpMinimumVersion = DateTime.ParseExact(YtDlpMinimumVersionString, "yyyy.MM.dd", CultureInfo.InvariantCulture);

        private static readonly HttpClient HttpClient;
        private static readonly SemaphoreSlim Gate = new(1, 1);

        internal static string ToolsDirectory { get; } = AppPaths.GetDataSubdirectory("Tools");
        internal static string YtDlpPath => Path.Combine(ToolsDirectory, YtDlpExecutableName);
        internal static string FfmpegPath => Path.Combine(ToolsDirectory, FfmpegExecutableName);
        internal static string NodePath => Path.Combine(ToolsDirectory, NodeExecutableName);

        private static bool _initialized;
        internal static Action<string>? Logger { get; set; }

        static ExternalToolInstaller()
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DnDOverlay/1.0 (+https://github.com/)");
        }

        internal static async Task<bool> EnsureToolsAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized && File.Exists(YtDlpPath) && File.Exists(FfmpegPath))
            {
                return true;
            }

            await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(ToolsDirectory);

                Log("Ensuring external tools...");
                var ytReady = await EnsureYtDlpAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var ffReady = await EnsureFfmpegAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var nodeReady = await EnsureNodeAsync(cancellationToken).ConfigureAwait(false);

                _initialized = ytReady && ffReady && nodeReady;
                Log($"External tools ready: yt-dlp={ytReady}, ffmpeg={ffReady}, node={nodeReady}");
                return _initialized;
            }
            catch (Exception ex)
            {
                Log($"EnsureToolsAsync failed: {ex.Message}");
                return false;
            }
            finally
            {
                Gate.Release();
            }
        }

        private static async Task<bool> EnsureNodeAsync(CancellationToken cancellationToken)
        {
            try
            {
                Log("Checking Node.js runtime...");
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(NodePath))
                {
                    var info = new FileInfo(NodePath);
                    if (info.Length > 0)
                    {
                        Log("Node.js already present.");
                        return true;
                    }

                    File.Delete(NodePath);
                }

                Log("Downloading Node.js...");
                var tempFile = Path.GetTempFileName();
                try
                {
                    await DownloadFileAsync(NodeDownloadUrl, tempFile, cancellationToken).ConfigureAwait(false);

                    using var archive = ZipFile.OpenRead(tempFile);
                    var nodeEntry = archive.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), NodeArchiveExecutablePath, StringComparison.OrdinalIgnoreCase));
                    if (nodeEntry == null)
                    {
                        Log("Node.js executable not found in archive.");
                        return false;
                    }

                    await using var entryStream = nodeEntry.Open();
                    await using var output = File.Create(NodePath);
                    await entryStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                    return File.Exists(NodePath);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                        // ignore cleanup failures
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"EnsureNodeAsync failed: {ex.Message}");
                return false;
            }
        }

        internal static void ApplyEnvironment(ProcessStartInfo psi)
        {
            if (psi == null)
            {
                return;
            }

            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var toolPath = ToolsDirectory;
            if (!string.IsNullOrWhiteSpace(toolPath))
            {
                psi.Environment["PATH"] = string.IsNullOrWhiteSpace(currentPath)
                    ? toolPath
                    : toolPath + ";" + currentPath;
            }
        }

        private static async Task<bool> EnsureYtDlpAsync(CancellationToken cancellationToken)
        {
            try
            {
                Log("Checking yt-dlp...");
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(YtDlpPath))
                {
                    var info = new FileInfo(YtDlpPath);
                    if (info.Length > 0)
                    {
                        if (IsYtDlpUpToDate(out var currentVersion))
                        {
                            Log("yt-dlp already present.");
                            return true;
                        }

                        Log($"yt-dlp version {(string.IsNullOrWhiteSpace(currentVersion) ? "unknown" : currentVersion)} is older than required {YtDlpMinimumVersionString}. Re-downloading...");
                        File.Delete(YtDlpPath);
                    }

                    File.Delete(YtDlpPath);
                }

                Log("Downloading yt-dlp...");
                await DownloadFileAsync(YtDlpDownloadUrl, YtDlpPath, cancellationToken).ConfigureAwait(false);
                return File.Exists(YtDlpPath);
            }
            catch (Exception ex)
            {
                Log($"EnsureYtDlpAsync failed: {ex.Message}");
                return false;
            }
        }

        private static bool IsYtDlpUpToDate(out string? versionString)
        {
            versionString = null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = YtDlpPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                ApplyEnvironment(psi);

                using var process = Process.Start(psi);
                if (process == null)
                {
                    Log("IsYtDlpUpToDate: failed to start yt-dlp for version check.");
                    return false;
                }

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(3000);

                if (string.IsNullOrWhiteSpace(output))
                {
                    Log("IsYtDlpUpToDate: yt-dlp returned empty version string.");
                    return false;
                }

                versionString = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                if (string.IsNullOrWhiteSpace(versionString))
                {
                    Log("IsYtDlpUpToDate: parsed version string is empty.");
                    return false;
                }

                if (!DateTime.TryParseExact(versionString, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var versionDate))
                {
                    Log($"IsYtDlpUpToDate: unable to parse version '{versionString}'.");
                    return true; // treat unknown versions as acceptable to avoid unnecessary re-downloads
                }

                return versionDate >= YtDlpMinimumVersion;
            }
            catch (Exception ex)
            {
                Log($"IsYtDlpUpToDate failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> EnsureFfmpegAsync(CancellationToken cancellationToken)
        {
            try
            {
                Log("Checking ffmpeg...");
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(FfmpegPath))
                {
                    var info = new FileInfo(FfmpegPath);
                    if (info.Length > 0)
                    {
                        Log("ffmpeg already present.");
                        return true;
                    }

                    File.Delete(FfmpegPath);
                }

                Log("Downloading ffmpeg archive...");
                var tempFile = Path.GetTempFileName();
                try
                {
                    await DownloadFileAsync(FfmpegDownloadUrl, tempFile, cancellationToken).ConfigureAwait(false);

                    using var archive = ZipFile.OpenRead(tempFile);
                    var binEntries = archive.Entries
                        .Where(e => !string.IsNullOrEmpty(e.FullName) &&
                                    e.FullName.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                    !e.FullName.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (binEntries.Count == 0)
                    {
                        Log("ffmpeg bin directory not found in archive.");
                        return false;
                    }

                    foreach (var entry in binEntries)
                    {
                        var fileName = Path.GetFileName(entry.FullName);
                        if (string.IsNullOrEmpty(fileName))
                        {
                            continue;
                        }

                        var destination = Path.Combine(ToolsDirectory, fileName);
                        Log($"Extracting {fileName}...");
                        await using var entryStream = entry.Open();
                        await using var output = File.Create(destination);
                        await entryStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (!File.Exists(FfmpegPath))
                    {
                        Log("ffmpeg.exe was not extracted.");
                        return false;
                    }

                    return File.Exists(FfmpegPath);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                        // ignore cleanup failures
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"EnsureFfmpegAsync failed: {ex.Message}");
                return false;
            }
        }

        private static async Task DownloadFileAsync(string url, string destination, CancellationToken cancellationToken)
        {
            Log($"Downloading {url} -> {destination}");
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = File.Create(destination);
            await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            Log($"Finished downloading to {destination}");
        }

        private static void Log(string message)
        {
            var line = $"[ExternalToolInstaller] {message}";
            Debug.WriteLine(line);
            Logger?.Invoke(line);
        }
    }
}
