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
                        continue;
                    }

                    string fileName = Path.GetFileName(filePath);
                    string fid = Program.getId(fileName);
                    if (string.IsNullOrEmpty(fid))
                    {
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
    }
}

