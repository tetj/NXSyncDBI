using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CopyUpdates
{
    // mtpmount-x64.exe mount Switch "4: Installed games" Y:

    partial class Program
    {
        // Entry point of the application.
        // Parses command-line arguments or prompts the user interactively, then runs the appropriate sync mode.
        // In MTP mode, the direction (push vs pull) is inferred from whether the origin path starts with a backslash.
        static void Main(
            string[] args) // command-line arguments passed to the application.
        {
            string originPath;
            string destinationPath;
            bool compareMode = false;
            bool uploadAll = false;

            if (args.Length > 0)
            {
                // Parse command line arguments
                (originPath, destinationPath, compareMode, uploadAll) = ParseCommandLineArguments(args);
                if (string.IsNullOrEmpty(originPath) || string.IsNullOrEmpty(destinationPath))
                {
                    ShowHelp();
                    return;
                }
            }
            else
            {
                // Interactive mode
                Console.WriteLine("Running in interactive mode...\n");
                originPath = GetValidFolderPath("Enter the origin folder path: ", true);
                if (string.IsNullOrEmpty(originPath))
                {
                    return;
                }

                destinationPath = GetValidFolderPath("Enter the destination folder path: ", true);
                if (string.IsNullOrEmpty(destinationPath))
                {
                    return;
                }
            }

            // Auto-detect MTP mode: if either path starts with a backslash, it refers to an MTP device path.
            bool mtpMode = originPath.StartsWith(@"\") || destinationPath.StartsWith(@"\");

            try
            {
                if (mtpMode)
                {
                    if (!originPath.StartsWith(@"\"))
                    {
                        // Push mode: local backup → MTP device
                        try
                        {
                            originPath = Path.GetFullPath(originPath);
                        }
                        catch
                        {
                        }
                        if (!Directory.Exists(originPath))
                        {
                            Console.WriteLine($"Error: Origin directory does not exist: {originPath}");
                            return;
                        }
                        UploadToMtp(originPath, destinationPath, @"\4: Installed games", null, uploadAll);
                    }
                    else
                    {
                        // Pull mode: MTP device → local backup
                        try
                        {
                            destinationPath = Path.GetFullPath(destinationPath);
                        }
                        catch
                        {
                        }
                        if (!Directory.Exists(destinationPath))
                        {
                            Console.WriteLine($"Error: Destination directory does not exist: {destinationPath}");
                            return;
                        }
                        DownloadFromMtp(originPath, destinationPath, null);
                    }
                    return;
                }

                // Local mode: normalize both paths
                originPath = Path.GetFullPath(originPath);
                destinationPath = Path.GetFullPath(destinationPath);

                // Validate paths
                if (!ValidatePaths(originPath, destinationPath))
                {
                    return;
                }

                // Get list of folders in origin
                List<string> originFolders = new List<string>();
                originFolders.AddRange(Directory.GetDirectories(originPath));

                int totalFolders = originFolders.Count;

                if (totalFolders == 0)
                {
                    Console.WriteLine("\nNo folders found in origin directory.");
                    WaitForKeyPressIfInteractive(args);
                    return;
                }

                if (compareMode)
                {
                    int totalMismatches = 0;
                    for (int i = 0; i < originFolders.Count; i++)
                    {
                        totalMismatches += Compare(destinationPath, originFolders, totalFolders, i, mtpMode);
                    }
                    if (totalMismatches == 0)
                    {
                        Console.WriteLine("All files are an exact match.");
                    }
                }
                else
                {
                    int nbSucessfullyCopied = 0;

                    // Pre-build a map: file ID -> destination folder, by scanning all dest subfolders
                    var idToDestFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string destFolder in Directory.GetDirectories(destinationPath))
                    {
                        foreach (string f in Directory.GetFiles(destFolder, "*.*", System.IO.SearchOption.AllDirectories))
                        {
                            string fid = getId(Path.GetFileName(f));
                            if (!string.IsNullOrEmpty(fid) && !idToDestFolder.ContainsKey(fid))
                            {
                                idToDestFolder[fid] = Path.GetDirectoryName(f);
                            }
                        }
                    }

                    // Process each origin folder, matching it to a destination folder by shared file ID
                    for (int i = 0; i < originFolders.Count; i++)
                    {
                        string originFolder = originFolders[i];
                        DrawProgressBar(i + 1, totalFolders);

                        string matchedDestFolder = null;
                        string matchedFid = null;
                        foreach (string f in Directory.GetFiles(originFolder, "*.*", System.IO.SearchOption.TopDirectoryOnly))
                        {
                            string fid = getId(Path.GetFileName(f));
                            if (string.IsNullOrEmpty(fid))
                            {
                                continue;
                            }

                            if (idToDestFolder.TryGetValue(fid, out string dest))
                            {
                                matchedDestFolder = dest;
                                matchedFid = fid;
                                Console.WriteLine("\nMatch found for " + originFolder);
                                break;
                            }

                            // Fallback: match by 12-character ID prefix (covers base↔update/DLC ID variants)
                            string prefix = GetTitlePrefix(fid);
                            var kvp = idToDestFolder.FirstOrDefault(
                                kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                            if (kvp.Key != null)
                            {
                                matchedDestFolder = kvp.Value;
                                matchedFid = fid;
                                Console.WriteLine("\nMatch found (prefix) for " + originFolder);
                                break;
                            }
                        }

                        if (matchedDestFolder != null)
                        {
                            string expectedPrefix = GetTitlePrefix(matchedFid);
                            nbSucessfullyCopied =+ SynchronizeFolders(originFolder, matchedDestFolder, expectedPrefix, mtpMode);
                        }
                    }

                    //Console.WriteLine("\nSync completed successfully!");

                    // Process loose files directly in originPath
                    MoveOriginFilesToDestination(originPath, idToDestFolder, mtpMode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError occurred: {ex.Message}");
                Environment.ExitCode = 1;
            }

            WaitForKeyPressIfInteractive(args);
        }

        // Parses the command-line arguments into individual settings used to control the sync operation.
        // Returns: a tuple containing originPath, destinationPath, and compareMode.
        static (string originPath, string destinationPath, bool compareMode, bool uploadAll) ParseCommandLineArguments(
            string[] args) // the array of command-line argument strings.
        {
            string originPath = null;
            string destinationPath = null;
            bool compareMode = false;
            bool uploadAll = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                switch (arg)
                {
                    case "-o":
                    case "--origin":
                        if (i + 1 < args.Length)
                        {
                            originPath = args[++i];
                        }
                        break;

                    case "-d":
                    case "--destination":
                        if (i + 1 < args.Length)
                        {
                            destinationPath = args[++i];
                        }
                        break;

                    case "-c":
                    case "--compare":
                        compareMode = true;
                        break;

                    case "-all":
                    case "--all":
                        uploadAll = true;
                        break;

                    case "-h":
                    case "--help":
                        ShowHelp();
                        return (null, null, false, false);

                    default:
                        // If not using named parameters, assume first two args are origin and destination
                        if (args.Length == 2 && i == 0)
                        {
                            originPath = args[0];
                            destinationPath = args[1];
                            return (originPath, destinationPath, false, false);
                        }
                        break;
                }
            }

            return (originPath, destinationPath, compareMode, uploadAll);
        }

        // Prints usage instructions and all available command-line options to the console.
        static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  CopyUpdates.exe -o <origin> -d <destination> [-c]");
            Console.WriteLine("  CopyUpdates.exe <origin> <destination>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -o, --origin       Origin folder (contains subfolders of game files)");
            Console.WriteLine("  -d, --destination  Destination folder (contains subfolders of game files)");
            Console.WriteLine("  -c, --compare      Compare mode: scan for missing or size-mismatched files and replace them");
            Console.WriteLine("  -all, --all        Upload all games to the Switch, not just updates and DLCs");
            Console.WriteLine("                     Base games are copied only when they are not already installed.");
            Console.WriteLine("  -h, --help         Show this help message");
            Console.WriteLine();
            Console.WriteLine("Sync mode (default):");
            Console.WriteLine("  Matches each origin subfolder to a destination subfolder by shared file ID.");
            Console.WriteLine("  A file is copied when no file with the same ID exists in the destination,");
            Console.WriteLine("  or when the source version [vN] is higher than all destination versions.");
            Console.WriteLine("  Older destination versions are sent to the Recycle Bin before copying.");
            Console.WriteLine("  Empty files are skipped.");
            Console.WriteLine();
            Console.WriteLine("Compare mode (-c):");
            Console.WriteLine("  Scans every file in the origin by ID and locates its counterpart in the destination.");
            Console.WriteLine("  Reports files with no match or a size difference greater than 3000 bytes,");
            Console.WriteLine("  and automatically replaces mismatched files.");
            Console.WriteLine();
            Console.WriteLine("File naming convention:");
            Console.WriteLine("  Files must embed a bracketed ID and optional version, e.g.");
            Console.WriteLine("  Game Title [0100XXXXXXXX0000][v131072].nsp");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  CopyUpdates.exe -o \"\\4: Installed games\" -d T:\\Backup\\         (Download updates from Switch: origin starts with \\)");
            Console.WriteLine("  CopyUpdates.exe -o C:\\NewGames\\ -d \"\\5: SD Card install\"       (Upload updates to Switch: dest starts with \\)");
            Console.WriteLine("  CopyUpdates.exe -o C:\\NewGames\\ -d C:\\AllMyGames\\               (local: no backslash prefix)");
            Console.WriteLine();
            Console.WriteLine("If no arguments are provided, the application runs in interactive mode.");
        }

        // Checks that both the origin and destination directories exist on disk.
        // Returns: true if both directories exist; false if either is missing (an error message is printed).
        static bool ValidatePaths(
            string originPath,      // path of the origin directory.
            string destinationPath) // path of the destination directory.
        {
            if (!Directory.Exists(originPath))
            {
                Console.WriteLine($"Error: Origin directory does not exist: {originPath}");
                return false;
            }

            if (!Directory.Exists(destinationPath))
            {
                Console.WriteLine($"Error: Destination directory does not exist: {destinationPath}");
                return false;
            }

            return true;
        }

        // Prompts the user to enter a folder path and validates it, looping until a valid path is given.
        // Returns: the validated folder path entered by the user.
        static string GetValidFolderPath(
            string prompt,  // the message displayed to the user.
            bool mustExist) // when true, the entered path must refer to an existing directory.
        {
            while (true)
            {
                Console.Write(prompt);
                string path = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(path))
                {
                    Console.WriteLine("Path cannot be empty. Please try again or press Ctrl+C to exit.");
                    continue;
                }

                if (mustExist && !Directory.Exists(path))
                {
                    Console.WriteLine("Directory does not exist. Please try again or press Ctrl+C to exit.");
                    continue;
                }

                return path;
            }
        }

        // Renders a text progress bar to the console showing how many items have been processed.
        static void DrawProgressBar(
            int current,      // the number of items processed so far.
            int total,        // the total number of items to process.
            int barSize = 40) // the width of the progress bar in characters (default: 40).
        {
            int progress = (int)((double)current / total * barSize);
            int percentage = (int)((double)current / total * 100);

            //Console.Write("\r[");
            //Console.Write(new string('#', progress));
            //Console.Write(new string('-', barSize - progress));
            //Console.Write($"] {percentage}%");
        }

        // Waits for a key press before exiting, but only when the application is running in interactive mode.
        static void WaitForKeyPressIfInteractive(
            string[] args) // the original command-line arguments; an empty array means interactive mode.
        {
            // Only wait for key press if running in interactive mode
            if (args.Length == 0)
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
