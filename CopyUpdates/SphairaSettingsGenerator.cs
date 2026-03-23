using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CopyUpdates
{
    /// <summary>
    /// Scans a local directory structure and generates a single sphaira settings.json file
    /// that tags each base game as "coop" or "single" based on its folder hierarchy.
    ///
    /// The directory tree is expected to follow the agerating layout produced by -agerating:
    ///   inputPath\
    ///     1\
    ///       EVERYONE\
    ///         _SDCARD1\
    ///           Mario Odyssey[010000000000000][v0].nsp        ← single-player
    ///     COOP\
    ///       EVERYONE\
    ///         _SDCARD1\
    ///           Mario Kart 8[0100152000022000][v0].nsp        ← coop
    ///
    /// Usage (SD card folder mode, mirrors -ulaunch _SDCARD1):
    ///   CopyUpdates.exe -sphaira _SDCARD1 -o T:\Backups\titles\ -d C:\sphaira_out\
    ///
    /// Usage (full directory scan):
    ///   CopyUpdates.exe -sphaira -o T:\Backups\titles\ -d C:\sphaira_out\
    ///
    /// Output:
    ///   C:\sphaira_out\settings.json
    /// Copy settings.json to /switch/sphaira/ on your Switch SD card.
    /// </summary>
    internal class SphairaSettingsGenerator
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        // Matches exactly 16 hex characters inside square brackets: e.g. [01007EF00011E000]
        private static readonly Regex TitleIdPattern =
            new(@"\[([0-9A-Fa-f]{16})\]", RegexOptions.Compiled);

        // ─── Public API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Recursively searches <paramref name="inputPath"/> for every directory named
        /// <paramref name="sdCardFolderName"/> and classifies its base games as "coop" or
        /// "single" by examining whether a parent path segment equals "COOP" or "1".
        /// Returns a dictionary mapping lowercase title ID → tag ("coop" or "single").
        /// </summary>
        public Dictionary<string, string> ScanBySdCardFolder(string inputPath, string sdCardFolderName)
        {
            if (!Directory.Exists(inputPath))
            {
                throw new DirectoryNotFoundException($"Input directory not found: {inputPath}");
            }

            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string sdCardDir in Directory.EnumerateDirectories(inputPath, sdCardFolderName, SearchOption.AllDirectories).Order())
            {
                string? category = ClassifyByParentPath(sdCardDir);
                if (category == null)
                {
                    Console.WriteLine($"[Skip] Could not classify (no '1' or 'COOP' parent found): {sdCardDir}");
                    continue;
                }

                string tag = category == "COOP" ? "coop" : "single";

                foreach (string file in Directory.EnumerateFiles(sdCardDir).Order())
                {
                    string ext = Path.GetExtension(file);
                    if (!ext.Equals(".nsp", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".nsz", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(file);
                    if (IsUpdateOrDlc(fileName))
                    {
                        continue;
                    }

                    string? titleId = ExtractTitleId(fileName);
                    if (titleId == null)
                    {
                        continue;
                    }

                    string titleIdLower = titleId.ToLowerInvariant();
                    if (!tags.ContainsKey(titleIdLower))
                    {
                        tags[titleIdLower] = tag;
                    }
                }
            }

            return tags;
        }

        /// <summary>
        /// Recursively scans all .nsp/.nsz base game files under <paramref name="inputPath"/>
        /// and classifies each as "coop" or "single" based on whether its directory path
        /// contains a segment equal to "COOP" or "1". Files that cannot be classified are skipped.
        /// Returns a dictionary mapping lowercase title ID → tag ("coop" or "single").
        /// </summary>
        public Dictionary<string, string> ScanDirectory(string inputPath)
        {
            if (!Directory.Exists(inputPath))
            {
                throw new DirectoryNotFoundException($"Input directory not found: {inputPath}");
            }

            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories).Order())
            {
                string ext = Path.GetExtension(file);
                if (!ext.Equals(".nsp", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".nsz", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName = Path.GetFileName(file);
                if (IsUpdateOrDlc(fileName))
                {
                    continue;
                }

                string? titleId = ExtractTitleId(fileName);
                if (titleId == null)
                {
                    continue;
                }

                string? category = ClassifyByParentPath(Path.GetDirectoryName(file)!);
                if (category == null)
                {
                    continue;
                }

                string tag = category == "COOP" ? "coop" : "single";
                string titleIdLower = titleId.ToLowerInvariant();
                if (!tags.ContainsKey(titleIdLower))
                {
                    tags[titleIdLower] = tag;
                }
            }

            return tags;
        }

        /// <summary>
        /// Writes a sphaira <c>settings.json</c> file into <paramref name="outputPath"/>.
        /// Copy the resulting file to <c>/switch/sphaira/</c> on your Switch SD card.
        /// </summary>
        public void Generate(string outputPath, Dictionary<string, string> titleTags)
        {
            Directory.CreateDirectory(outputPath);

            var settings = new SphairaSettings
            {
                PinnedApps = new List<string>(),
                Tags = titleTags.ToDictionary(
                    kv => kv.Key,
                    kv => new List<string> { kv.Value }),
                SortKind = 0,
                ShowTitles = true,
                IconCacheEnabled = true,
                ThemePath = "sdmc:/switch/sphaira/themes/default.json",
                GridSize = 6
            };

            string outputFile = Path.Combine(outputPath, "settings.json");
            File.WriteAllText(outputFile, JsonSerializer.Serialize(settings, JsonOptions));

            int coopCount   = titleTags.Count(kv => kv.Value == "coop");
            int singleCount = titleTags.Count(kv => kv.Value == "single");
            Console.WriteLine($"[Sphaira] {outputFile}");
            Console.WriteLine($"  {coopCount} COOP game(s), {singleCount} single-player game(s).");
            Console.WriteLine($"Done. Copy \"{outputFile}\" to /switch/sphaira/ on your Switch SD card.");
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inspects the path segments of <paramref name="dirPath"/> to determine the
        /// player-count category. Returns "COOP" when a segment equals "COOP"
        /// (case-insensitive), "SINGLE" when a segment equals "1", or <c>null</c> if
        /// neither is found. COOP is checked first so it wins if both appear.
        /// </summary>
        private static string? ClassifyByParentPath(string dirPath)
        {
            string[] segments = dirPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool hasSingle = false;

            foreach (string segment in segments)
            {
                if (segment.Equals("COOP", StringComparison.OrdinalIgnoreCase))
                {
                    return "COOP";
                }
                if (segment.Equals("1", StringComparison.Ordinal))
                {
                    hasSingle = true;
                }
            }

            return hasSingle ? "SINGLE" : null;
        }

        /// <summary>Extracts the 16-char hex title ID from a bracketed segment of the filename.</summary>
        private static string? ExtractTitleId(string fileName)
        {
            var match = TitleIdPattern.Match(fileName);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Returns true for update and DLC files that must not produce a sphaira tag entry.
        /// </summary>
        private static bool IsUpdateOrDlc(string fileName)
        {
            return fileName.Contains("[UPD]",    StringComparison.OrdinalIgnoreCase) ||
                   fileName.Contains("[UPDATE]", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Contains("[DLC]",    StringComparison.OrdinalIgnoreCase);
        }
    }

    // ─── JSON serialisation shape ─────────────────────────────────────────────────────

    internal class SphairaSettings
    {
        [JsonPropertyName("pinned_apps")]
        public List<string> PinnedApps { get; set; } = new();

        [JsonPropertyName("tags")]
        public Dictionary<string, List<string>> Tags { get; set; } = new();

        [JsonPropertyName("sort_kind")]
        public int SortKind { get; set; } = 0;

        [JsonPropertyName("show_titles")]
        public bool ShowTitles { get; set; } = true;

        [JsonPropertyName("icon_cache_enabled")]
        public bool IconCacheEnabled { get; set; } = true;

        [JsonPropertyName("theme_path")]
        public string ThemePath { get; set; } = "sdmc:/switch/sphaira/themes/default.json";

        [JsonPropertyName("grid_size")]
        public int GridSize { get; set; } = 6;
    }
}
