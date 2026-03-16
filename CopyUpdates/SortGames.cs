using MediaDevices;
using Microsoft.VisualBasic.FileIO;

namespace CopyUpdates
{
    internal class SortGames
    {
        private const string InstalledFolderName = "_Installed";
        private const string UninstalledFolderName = "_NotInstalled";

        // Connects to the MTP device, scans mtpOriginPath for installed games, then moves every
        // recognized game file under destinationPath into the correct _Installed or _Uninstalled subfolder.
        public void Run(
            string mtpOriginPath,   // MTP path to scan for installed games (e.g. "\4: Installed games").
            string destinationPath) // local root path containing the game files to sort.
        {
            var allDevices = MediaDevice.GetDevices().ToList();
            if (allDevices.Count == 0)
            {
                Console.WriteLine("No MTP devices found. Make sure your Switch is connected and DBI MTP mode is active.");
                return;
            }

            MediaDevice device = allDevices.First();
            device.Connect();
            Console.WriteLine($"Connected to: {device.FriendlyName}");

            try
            {
                if (!Program.ScanMTP(mtpOriginPath, device, out HashSet<string> installedPrefixes, out _))
                {
                    return;
                }

                Console.WriteLine($"Sorting games in: {destinationPath}");

                string[] gameFiles = Directory.GetFiles(destinationPath, "*.*", System.IO.SearchOption.AllDirectories);
                int movedCount = 0;
                var sourceDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string filePath in gameFiles)
                {
                    string ext = Path.GetExtension(filePath);
                    if (!IsGameFile(ext))
                    {
                        Console.WriteLine($"SKIP (not game file): {Path.GetFileName(filePath)}");
                        continue;
                    }

                    string fileName = Path.GetFileName(filePath);
                    string fid = Program.getId(fileName);
                    if (string.IsNullOrEmpty(fid))
                    {
                        Console.WriteLine($"SKIP (no title ID): {fileName}");
                        continue;
                    }

                    string prefix = Program.GetTitlePrefix(fid);
                    bool isInstalled = installedPrefixes.Contains(prefix);

                    string newFilePath = ComputeNewFilePath(filePath, destinationPath, isInstalled);
                    if (newFilePath == null || string.Equals(newFilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newFilePath));
                        File.Move(filePath, newFilePath);
                        Console.WriteLine($"MOVED: {filePath}");
                        Console.WriteLine($"   TO: {newFilePath}");
                        movedCount++;
                        sourceDirectories.Add(Path.GetDirectoryName(filePath));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error moving {fileName}: {ex.Message}");
                    }
                }

                Console.WriteLine($"\n{movedCount} file(s) moved.");
                RemoveEmptyFolders(sourceDirectories, destinationPath);
            }
            finally
            {
                device.Disconnect();
            }
        }

        // Returns true when the file extension belongs to a recognized Nintendo Switch game format.
        private static bool IsGameFile(string extension)
        {
            string lower = extension.ToLowerInvariant();
            return lower == ".nsp" || lower == ".xci" || lower == ".nsz" || lower == ".xcz";
        }

        // Computes the target path for a file based on its installed status.
        // Scans each directory segment in the relative path for an install-status folder name
        // (e.g. "_Installed", "_NotInstalled"). When found and the status is wrong, replaces that
        // segment with the correct folder name and returns the new full path.
        // When no install-status segment is found, moves the file into a _Installed or _NotInstalled
        // subfolder under its current directory.
        // Returns null when the file is already in the right place.
        private static string ComputeNewFilePath(string filePath, string rootPath, bool isInstalled)
        {
            string relativePath = Path.GetRelativePath(rootPath, filePath);
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar);

            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (TryClassifyInstallFolder(segments[i], out bool currentlyInstalled))
                {
                    if (currentlyInstalled == isInstalled)
                    {
                        return null;
                    }

                    segments[i] = isInstalled ? InstalledFolderName : UninstalledFolderName;
                    string newRelativePath = string.Join(Path.DirectorySeparatorChar, segments);
                    return Path.Combine(rootPath, newRelativePath);
                }
            }

            // No install-status folder found in the path: place the file into _Installed or
            // _NotInstalled directly under its current directory.
            string targetFolder = isInstalled ? InstalledFolderName : UninstalledFolderName;
            return Path.Combine(Path.GetDirectoryName(filePath), targetFolder, Path.GetFileName(filePath));
        }

        // Determines whether a folder name represents an install-status folder.
        // Recognized patterns (case-insensitive): _Installed, _NotInstalled, _Uninstalled, etc.
        // Sets isInstalled to true for the "installed" variant, false for the "not installed" variant.
        // Returns true when the name is recognized as an install-status folder.
        private static bool TryClassifyInstallFolder(string folderName, out bool isInstalled)
        {
            string lower = folderName.ToLowerInvariant();
            if (lower.Contains("install"))
            {
                bool isNotInstalled = lower.Contains("not") || lower.Contains("uninstall");
                isInstalled = !isNotInstalled;
                return true;
            }

            isInstalled = false;
            return false;
        }

        // For each source directory, walks up the tree toward rootPath and sends to the Recycle Bin
        // any folder that is effectively empty (contains no files at any depth).
        // Processes deepest directories first so that emptied parents are caught in the same pass.
        private static void RemoveEmptyFolders(HashSet<string> sourceDirectories, string rootPath)
        {
            int removedCount = 0;

            foreach (string sourceDir in sourceDirectories.OrderByDescending(d => d.Length))
            {
                string current = sourceDir;
                while (current != null
                    && !string.Equals(current, rootPath, StringComparison.OrdinalIgnoreCase)
                    && current.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (!Directory.Exists(current) || !IsEffectivelyEmpty(current))
                    {
                        break;
                    }

                    string parent = Path.GetDirectoryName(current);
                    try
                    {
                        FileSystem.DeleteDirectory(current, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        Console.WriteLine($"REMOVED empty folder: {current}");
                        removedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error removing folder {current}: {ex.Message}");
                        break;
                    }

                    current = parent;
                }
            }

            if (removedCount > 0)
            {
                Console.WriteLine($"{removedCount} empty folder(s) sent to Recycle Bin.");
            }
        }

        // Returns true when the directory contains no files at any depth.
        // Directories that hold only empty subdirectories are considered empty.
        private static bool IsEffectivelyEmpty(string path)
        {
            return !Directory.EnumerateFiles(path, "*", System.IO.SearchOption.AllDirectories).Any();
        }

        // Appends [UPD] or [DLC] to filenames that are missing a content-type tag.
        // BASE files (title ID ends in 000) are left unchanged.
        // Only processes .nsp, .nsz, and .xci files that contain a bracketed 16-hex-char title ID.
        public void TagFiles(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Error: Directory does not exist: {path}");
                return;
            }

            Console.WriteLine($"Tagging files in: {path}");

            string[] gameFiles = Directory.GetFiles(path, "*.*", System.IO.SearchOption.AllDirectories);
            int renamedCount = 0;

            foreach (string filePath in gameFiles)
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext != ".nsp" && ext != ".nsz" && ext != ".xci")
                {
                    continue;
                }

                string fileName = Path.GetFileName(filePath);
                string fid = Program.getId(fileName);
                if (string.IsNullOrEmpty(fid))
                {
                    continue;
                }

                string suffix = fid.Substring(13, 3);
                string tag;
                if (suffix.Equals("000", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // BASE — no tag needed
                }
                else if (suffix.Equals("800", StringComparison.OrdinalIgnoreCase))
                {
                    tag = "[UPD]";
                }
                else
                {
                    tag = "[DLC]";
                }

                // Skip if the tag is already present in the filename.
                if (fileName.Contains(tag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Replace [Update] with [UPD] if present, otherwise append the tag.
                string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                bool replacedLongTag = false;
                if (tag == "[UPD]" && nameWithoutExt.Contains("[Update]", StringComparison.OrdinalIgnoreCase))
                {
                    int idx = nameWithoutExt.IndexOf("[Update]", StringComparison.OrdinalIgnoreCase);
                    nameWithoutExt = nameWithoutExt.Remove(idx, "[Update]".Length).Insert(idx, "[UPD]");
                    replacedLongTag = true;
                }

                string newFileName = replacedLongTag ? nameWithoutExt + ext : nameWithoutExt + tag + ext;
                string newFilePath = Path.Combine(Path.GetDirectoryName(filePath), newFileName);

                try
                {
                    File.Move(filePath, newFilePath);
                    Console.WriteLine($"TAGGED: {fileName}");
                    Console.WriteLine($"    AS: {newFileName}");
                    renamedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error renaming {fileName}: {ex.Message}");
                }
            }

            Console.WriteLine($"\n{renamedCount} file(s) renamed.");
        }

        // Moves base game and update files out of game subfolders and into their _Installed or
        // _NotInstalled parent folder. DLC files are left in place. Files already sitting directly
        // inside the install-status folder are skipped. Empty game subfolders are sent to the Recycle Bin.
        public void FlattenFolders(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Error: Directory does not exist: {path}");
                return;
            }

            Console.WriteLine($"Flattening folders in: {path}");

            string[] gameFiles = Directory.GetFiles(path, "*.*", System.IO.SearchOption.AllDirectories);
            int movedCount = 0;
            var sourceDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in gameFiles)  
            {
                string ext = Path.GetExtension(filePath);
                if (!IsGameFile(ext))
                {
                    Console.WriteLine($"SKIP (not game file): {Path.GetFileName(filePath)}");
                    continue;
                }

                string fileName = Path.GetFileName(filePath);
                string fid = Program.getId(fileName);
                if (string.IsNullOrEmpty(fid))
                {
                    Console.WriteLine($"SKIP (no title ID): {fileName}");
                    continue;
                }

                // Only move base games and updates; leave DLC files in place.
                if (!IsBaseOrUpdate(fid))
                {
                    Console.WriteLine($"SKIP (DLC [{fid}]): {fileName}");
                    continue;
                }

                string fileDir = Path.GetDirectoryName(filePath);
                string installFolder = FindInstallStatusFolder(fileDir);
                if (installFolder == null)
                {
                    Console.WriteLine($"SKIP (no install-status parent): {fileName}");
                    continue;
                }

                // Already directly in the _Installed or _NotInstalled folder — nothing to do.
                if (string.Equals(fileDir, installFolder, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"SKIP (already flat): {fileName}");
                    continue;
                }

                string newFilePath = Path.Combine(installFolder, fileName);

                try
                {
                    File.Move(filePath, newFilePath);
                    Console.WriteLine($"MOVED: {filePath}");
                    Console.WriteLine($"   TO: {newFilePath}");
                    movedCount++;
                    sourceDirectories.Add(fileDir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error moving {fileName}: {ex.Message}");
                }
            }

            Console.WriteLine($"\n{movedCount} file(s) moved.");

            // Use the nearest install-status folder that is AT OR BELOW path as the cleanup boundary,
            // so that game subfolders emptied by the move are recycled but the _Installed /
            // _NotInstalled folder itself is kept. Searching only within path (not above it) prevents
            // an ancestor directory whose name contains "install" from being used as the boundary,
            // which could otherwise allow the cleanup walk to delete path itself.
            string installFolderWithinPath = path != null && TryClassifyInstallFolder(Path.GetFileName(path), out _)
                ? path
                : null;
            string cleanupRoot = installFolderWithinPath ?? path;
            RemoveEmptyFolders(sourceDirectories, cleanupRoot);
        }

        // Returns true when the title ID belongs to a base game (last 3 hex digits are 000) or an update (last 3 are 800).
        // DLC title IDs end with any other suffix and return false.
        // Example: 010021C000B6A000 → last 3 = "000" → base game.
        //          010021C000B6A800 → last 3 = "800" → update.
        //          010021C000B6A001 → last 3 = "001" → DLC.
        private static bool IsBaseOrUpdate(string titleId)
        {
            if (titleId == null || titleId.Length != 16)
            {
                return false;
            }

            string suffix = titleId.Substring(13, 3);
            return suffix.Equals("000", StringComparison.OrdinalIgnoreCase)
                || suffix.Equals("800", StringComparison.OrdinalIgnoreCase);
        }

        // Walks up the directory tree from startDir and returns the first folder whose name
        // matches the install-status pattern (_Installed, _NotInstalled, etc.).
        // Returns null when no such folder is found.
        private static string FindInstallStatusFolder(string startDir)
        {
            string current = startDir;
            while (current != null)
            {
                if (TryClassifyInstallFolder(Path.GetFileName(current), out _))
                {
                    return current;
                }

                current = Path.GetDirectoryName(current);
            }

            return null;
        }
    }
}

