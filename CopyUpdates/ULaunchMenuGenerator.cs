using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AgeRatingSorter
{
    /// <summary>
    /// Scans a real directory structure on your PC and generates the corresponding
    /// uLaunch .m.json entry files and physical directories, ready to be copied to
    /// /ulaunch/menu/ on your Switch SD card.
    ///
    /// Expected input layout (.nsp/.nsz files live directly inside each collection folder):
    ///   inputPath\
    ///     Zelda Collection\
    ///       Game[01007EF00011E000][v0].nsp         ← base game  → game entry (type 1)
    ///       Game[01007EF00011E800][v196608][UPD].nsp ← update   → skipped
    ///       Game[01007EF00011F001][DLC].nsp          ← DLC      → skipped
    ///     Coop Collection\
    ///       Mario Kart 8[01237EF00011E123][v0].nsp
    ///       Puzzles\                               ← sub-folder → folder entry (type 3), recurses
    ///         Portal 2[01337EF00011E123][v0].nsp
    ///
    /// Generated output layout (mirrors /ulaunch/menu/):
    ///   outputPath\
    ///     0.m.json                     ← { "type":3, "name":"Zelda Collection", "fs_name":"Zelda_Collection" }
    ///     Zelda_Collection\
    ///       0.m.json                   ← { "type":1, "application_id":"01007EF00011E000" }
    ///     1.m.json                     ← { "type":3, "name":"Coop Collection", ... }
    ///     Coop_Collection\
    ///       0.m.json                   ← { "type":1, "application_id":"01237EF00011E123" }
    ///       1.m.json                   ← { "type":3, "name":"Puzzles", "fs_name":"Puzzles" }
    ///       Puzzles\
    ///         0.m.json                 ← { "type":1, "application_id":"01337EF00011E123" }
    /// </summary>
    internal class ULaunchMenuGenerator
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
        /// Walks <paramref name="inputPath"/>: each immediate subdirectory becomes a
        /// top-level uLaunch folder. Scanning is recursive to support nested sub-folders.
        /// </summary>
        public List<ULaunchFolder> ScanDirectory(string inputPath)
        {
            if (!Directory.Exists(inputPath))
            {
                throw new DirectoryNotFoundException($"Input directory not found: {inputPath}");
            }

            return Directory.EnumerateDirectories(inputPath)
                            .Order()
                            .Select(d => ScanFolder(d))
                            .Where(f => !f.IsEmpty)        // ← skip top-level folders with nothing in them
                            .ToList();
        }

        /// <summary>
        /// Writes the uLaunch directory structure and .m.json entry files to
        /// <paramref name="outputPath"/>. Copy its contents to /ulaunch/menu/ on your SD card.
        /// </summary>
        public void Generate(string outputPath, IReadOnlyList<ULaunchFolder> folders)
        {
            Directory.CreateDirectory(outputPath);

            for (int i = 0; i < folders.Count; i++)
            {
                var folder = folders[i];

                // Folder entry in the menu root
                string folderEntryPath = Path.Combine(outputPath, $"{i}.m.json");
                var folderEntry = new ULaunchFolderEntry
                {
                    Type   = 3,
                    Name   = folder.DisplayName,
                    FsName = folder.FsName
                };

                File.WriteAllText(folderEntryPath, JsonSerializer.Serialize(folderEntry, JsonOptions));
                Console.WriteLine($"[Folder] {folderEntryPath}");
                Console.WriteLine($"         \"{folder.DisplayName}\"  →  {folder.FsName}/");

                // Recurse into the physical directory for this folder
                string physicalDir = Path.Combine(outputPath, folder.FsName);
                GenerateFolder(physicalDir, folder, depth: 1);
            }

            Console.WriteLine();
            Console.WriteLine($"Done. Copy the contents of \"{outputPath}\" to /ulaunch/menu/ on your SD card.");
        }

        // ─── Scanning (recursive) ─────────────────────────────────────────────────────

        /// <summary>
        /// Scans <paramref name="folderPath"/> for .nsp/.nsz base-game files (→ game entries)
        /// and subdirectories (→ nested uLaunch folders), recursively.
        /// </summary>
        private static ULaunchFolder ScanFolder(string folderPath)
        {
            var folder       = new ULaunchFolder(Path.GetFileName(folderPath));
            var seenTitleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // .nsp / .nsz files directly in this folder → game entries
            foreach (string file in Directory.EnumerateFiles(folderPath).Order())
            {
                string ext = Path.GetExtension(file);

                if (!ext.Equals(".nsp", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".nsz", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName = Path.GetFileName(file);

                // Updates and DLC share the same collection folder but must not create their own uLaunch entry
                if (IsUpdateOrDlc(fileName))
                {
                    continue;
                }

                string? titleId = ExtractTitleId(fileName);
                if (titleId == null)
                {
                    continue;
                }

                // One uLaunch entry per unique title ID (guard against duplicate base files)
                if (!seenTitleIds.Add(titleId))
                {
                    continue;
                }

                folder.Games.Add(new ULaunchGameItem(ExtractGameName(fileName), titleId));
            }

            // Subdirectories → nested uLaunch folders (recurse)
            foreach (string subDir in Directory.EnumerateDirectories(folderPath).Order())
            {
                var subFolder = ScanFolder(subDir);

                if (!subFolder.IsEmpty)                // ← skip nested folders with nothing in them
                {
                    folder.SubFolders.Add(subFolder);
                }
            }

            return folder;
        }

        // ─── Output generation (recursive) ────────────────────────────────────────────

        private void GenerateFolder(string outputFolderPath, ULaunchFolder folder, int depth)
        {
            Directory.CreateDirectory(outputFolderPath);

            string indent = new string(' ', depth * 2);
            int    index  = 0;

            // Game entries first, in the order they were scanned
            foreach (var game in folder.Games)
            {
                string applicationId = game.ApplicationId ?? "TODO_FILL_IN";

                var gameEntry = new ULaunchRetailGameEntry
                {
                    Type          = 1,
                    ApplicationId = applicationId
                };

                string entryPath = Path.Combine(outputFolderPath, $"{index}.m.json");
                File.WriteAllText(entryPath, JsonSerializer.Serialize(gameEntry, JsonOptions));

                string status = game.ApplicationId != null ? "✓" : "⚠ no ID";
                Console.WriteLine($"{indent}[{status}] {entryPath}");
                Console.WriteLine($"{indent}      \"{game.DisplayName}\"  →  {applicationId}");

                index++;
            }

            // Subfolder entries follow, each recursing into its own directory
            foreach (var subFolder in folder.SubFolders)
            {
                var folderEntry = new ULaunchFolderEntry
                {
                    Type   = 3,
                    Name   = subFolder.DisplayName,
                    FsName = subFolder.FsName
                };

                string entryPath = Path.Combine(outputFolderPath, $"{index}.m.json");
                File.WriteAllText(entryPath, JsonSerializer.Serialize(folderEntry, JsonOptions));
                Console.WriteLine($"{indent}[Folder] {entryPath}  →  \"{subFolder.DisplayName}\" ({subFolder.FsName}/)");

                string subFolderOutputPath = Path.Combine(outputFolderPath, subFolder.FsName);
                GenerateFolder(subFolderOutputPath, subFolder, depth + 1);

                index++;
            }
        }

        // ─── Filename parsing ─────────────────────────────────────────────────────────

        /// <summary>Extracts the 16-char hex title ID from a bracketed segment of the filename.</summary>
        private static string? ExtractTitleId(string fileName)
        {
            var match = TitleIdPattern.Match(fileName);
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
        }

        /// <summary>Extracts the human-readable game name — everything before the first '['.</summary>
        private static string ExtractGameName(string fileName)
        {
            int bracketIdx = fileName.IndexOf('[');
            string raw = bracketIdx > 0
                ? fileName[..bracketIdx]
                : Path.GetFileNameWithoutExtension(fileName);
            return raw.Trim();
        }

        /// <summary>
        /// Returns true for update and DLC files that must not produce a uLaunch game entry.
        /// Checked against the [TYPE] segment in the filename.
        /// </summary>
        private static bool IsUpdateOrDlc(string fileName)
        {
            return fileName.Contains("[UPD]",    StringComparison.OrdinalIgnoreCase) ||
                   fileName.Contains("[UPDATE]", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Contains("[DLC]",    StringComparison.OrdinalIgnoreCase);
        }
    }

    // ─── Domain model ─────────────────────────────────────────────────────────────────

    internal class ULaunchFolder
    {
        public string DisplayName { get; }
        public string FsName { get; }

        /// <summary>Base-game NSP/NSZ files found directly inside this folder.</summary>
        public List<ULaunchGameItem> Games { get; } = new();

        /// <summary>Subdirectories that become nested uLaunch folders.</summary>
        public List<ULaunchFolder> SubFolders { get; } = new();

        public ULaunchFolder(string displayName)
        {
            DisplayName = displayName;
            FsName      = ToFsName(displayName);
        }

        /// <summary>
        /// Converts a display name to a FAT32-safe filesystem name.
        /// Spaces become underscores; characters illegal on FAT32 are removed.
        /// </summary>
        private static string ToFsName(string name)
        {
            var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
            var sb           = new StringBuilder();

            foreach (char c in name)
            {
                if (c == ' ')
                {
                    sb.Append('_');
                }
                else if (!invalidChars.Contains(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Trim('_');
        }

        public bool IsEmpty => Games.Count == 0 && SubFolders.Count == 0;
    }

    internal class ULaunchGameItem(string displayName, string? applicationId)
    {
        public string  DisplayName   { get; } = displayName;

        /// <summary>Title ID (hex, e.g. "01007EF00011E000"). Null when no .nsp/.nsz was found.</summary>
        public string? ApplicationId { get; } = applicationId;
    }

    // ─── JSON serialisation shapes ────────────────────────────────────────────────────

    internal class ULaunchFolderEntry
    {
        [JsonPropertyName("type")]    public int    Type   { get; set; }
        [JsonPropertyName("name")]    public string Name   { get; set; } = string.Empty;
        [JsonPropertyName("fs_name")] public string FsName { get; set; } = string.Empty;
    }

    internal class ULaunchRetailGameEntry
    {
        [JsonPropertyName("type")]           public int    Type          { get; set; }
        [JsonPropertyName("application_id")] public string ApplicationId { get; set; } = string.Empty;
    }
}

