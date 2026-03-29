using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CopyUpdates
{
    internal class MoveWithJson
    {
        public List<Game> games { get; private set; }
        private readonly string _destinationDir;
        private readonly bool _verbose;

        private static readonly Uri JsonSource = new Uri("https://raw.githubusercontent.com/blawar/titledb/master/US.en.json");

        public MoveWithJson(string destinationDir, bool verbose = false)
        {
            _destinationDir = destinationDir;
            _verbose = verbose;
            games = new List<Game>();
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "US.en.json");
            // Ensure we have a fresh-enough copy of the JSON (download if missing or older than a week)
            DownloadIfNeededAsync(jsonPath).GetAwaiter().GetResult();
            games = TitleDbParser.Parse(jsonPath);
        }

        private void Log(string message)
        {
            if (_verbose)
            {
                Console.WriteLine(message);
            }
        }

        private async Task DownloadIfNeededAsync(string jsonPath)
        {
            try
            {
                bool shouldDownload = true;

                if (File.Exists(jsonPath))
                {
                    var lastWrite = File.GetLastWriteTimeUtc(jsonPath);
                    if (DateTime.UtcNow - lastWrite < TimeSpan.FromDays(7))
                    {
                        shouldDownload = false;
                    }
                }

                if (!shouldDownload)
                    return;

                using var client = new HttpClient();
                using var resp = await client.GetAsync(JsonSource);
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Failed to download JSON from {JsonSource} - {resp.StatusCode}");
                    return;
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                var dir = Path.GetDirectoryName(jsonPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllBytesAsync(jsonPath, bytes);
                Debug.WriteLine($"Downloaded updated JSON to {jsonPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading JSON: {ex.Message}");
            }
        }

        public void MoveOneGame(string folder, string gameName)
        {
            string? firstFile = Directory.GetFiles(folder, "*.nsp").OrderBy(f => Path.GetFileName(f)).FirstOrDefault();
            if (firstFile == null)
            {
                Log($"No .nsp files found in folder: {folder}");
                return;
            }
            string id = firstFile.Split('[', ']')[1];

            var game = games.FirstOrDefault(g => g.Id == id);

            if (game == null)
            {
                Log($"Game with ID {id} not found in the database.");
                return;
            }

            string targetDir = _destinationDir;

            if (game.NumberOfPlayers == 1)
            {
                targetDir = Path.Combine(targetDir, "1");
            }
            else
            {
                targetDir = Path.Combine(targetDir, "COOP");
            }

            string ageRating = "UNKNOWN";

            if (game.Rating == null || game.Rating <= 0)
            {
                ageRating = "UNKNOWN";
            }
            else if (game.Rating < 10)
            {
                ageRating = "EVERYONE";
            }
            else if (game.Rating < 13)
            {
                ageRating = "EVERYONE 10+";
            }
            else if (game.Rating < 17)
            {
                ageRating = "TEEN";
            }
            else
            {
                ageRating = "MATURE";
            }

            targetDir = Path.Combine(targetDir, ageRating, "_installed");

            if (!string.IsNullOrEmpty(targetDir))
            {
                Log($"Moving '{gameName}' -> '{targetDir}'");
                try
                {
                    FileHelper.MoveFolder(folder, targetDir, gameName);
                    Log($"Moved '{gameName}' to: {targetDir}");
                }
                catch (Exception ex)
                {
                    Log($"Error moving '{gameName}': {ex.Message}");
                }
            }
        }

        public void MoveFileWithNoMatch(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Log($"File not found: {filePath}");
                return;
            }

            string fileName = Path.GetFileName(filePath);
            string? id = null;
            int start = fileName.IndexOf('[');
            int end = start >= 0 ? fileName.IndexOf(']', start + 1) : -1;
            if (start >= 0 && end > start)
            {
                id = fileName.Substring(start + 1, end - start - 1);
            }

            if (string.IsNullOrEmpty(id))
            {
                Log($"Could not extract id from file name: {fileName}");
                return;
            }

            string sourceDir = Path.GetDirectoryName(filePath) ?? ".";
            if (IsUpdateOrDlc(fileName) && !HasBaseFile(sourceDir, id))
            {
                Log($"Skipping '{fileName}': no matching [BASE] file found in '{sourceDir}'.");
                return;
            }

            var game = games.FirstOrDefault(g => g.Id == id);
            if (game == null)
            {
                Log($"Game with ID {id} not found in the database.");
                return;
            }

            string targetDir = _destinationDir;

            if (game.NumberOfPlayers == 1)
            {
                targetDir = Path.Combine(targetDir, "1");
            }
            else
            {
                targetDir = Path.Combine(targetDir, "COOP");
            }

            string ageRating = "UNKNOWN";
            if (game.Rating < 7)
            {
                ageRating = "EVERYONE";
            }
            else if (game.Rating < 11)
            {
                ageRating = "EVERYONE 10+";
            }
            else if (game.Rating < 17)
            {
                ageRating = "TEEN";
            }
            else if (game.Rating > 16)
            {
                ageRating = "MATURE";
            }

            targetDir = Path.Combine(targetDir, ageRating);

            string gameName = fileName;
            int idx = gameName.IndexOf('[');
            if (idx >= 0)
                gameName = gameName.Substring(0, idx).Trim();

            string destFolder = targetDir;
            try
            {
                var relatedFiles = FindRelatedFiles(sourceDir, gameName);

                foreach (var src in relatedFiles)
                {
                    try
                    {
                        string srcFileName = Path.GetFileName(src);
                        string destPath = Path.Combine(destFolder, srcFileName);

                        if (File.Exists(destPath))
                        {
                            int counter = 1;
                            string nameOnly = Path.GetFileNameWithoutExtension(srcFileName);
                            string ext = Path.GetExtension(srcFileName);
                            string newPath;
                            do
                            {
                                newPath = Path.Combine(destFolder, $"{nameOnly} ({counter++}){ext}");
                            } while (File.Exists(newPath));

                            destPath = newPath;
                        }

                        Log($"Moving '{srcFileName}' -> '{destPath}'");
                        File.Move(src, destPath);
                        Log($"Moved '{srcFileName}' to: {destPath}");
                    }
                    catch (Exception innerEx)
                    {
                        Log($"Error moving '{src}': {innerEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error moving files for '{filePath}' to '{destFolder}': {ex.Message}");
            }
        }

        private IEnumerable<string> FindRelatedFiles(string directory, string namePrefix)
        {
            if (!Directory.Exists(directory))
                yield break;

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                string fn = Path.GetFileNameWithoutExtension(file);
                int idx = fn.IndexOf('[');
                string prefix = idx >= 0 ? fn.Substring(0, idx).Trim() : fn.Trim();
                if (string.Equals(prefix, namePrefix, StringComparison.OrdinalIgnoreCase))
                    yield return file;
            }
        }

        private static bool IsUpdateOrDlc(string fileName) =>
            fileName.Contains("[UPD]", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("[DLC]", StringComparison.OrdinalIgnoreCase);

        private static bool HasBaseFile(string directory, string titleId)
        {
            const int PrefixLength = 13;
            string prefix = titleId.Length >= PrefixLength
                ? titleId.Substring(0, PrefixLength)
                : titleId;

            return Directory.EnumerateFiles(directory)
                .Any(f =>
                    Path.GetFileName(f).Contains("[BASE]", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(f).Contains(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}
