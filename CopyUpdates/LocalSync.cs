using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CopyUpdates
{
    partial class Program
    {
        // Compares files in one origin folder against the destination tree and reports size mismatches.
        // For each mismatch larger than 3000 bytes the destination file is replaced with the origin file.
        // Returns: the number of mismatched files found.
        private static int Compare(
            string destinationPath,      // root path of the destination folder tree to search.
            List<string> originFolders,  // list of all origin folders being processed.
            int totalFolders,            // total number of folders, used for progress display.
            int i,                       // index of the current folder within originFolders.
            bool mtpMode = false)         // when true, uses stream copy (MTP-safe); when false, uses File.Move.
        {
            int mismatches = 0;
            try
            {
                string originFolder = originFolders[i];
                DrawProgressBar(i + 1, totalFolders);

                string[] files = Directory.GetFiles(originFolder, "*.*", System.IO.SearchOption.AllDirectories);
                foreach (string sourcePath in files)
                {
                    try
                    {
                        string id = getId(Path.GetFileName(sourcePath));
                        if (string.IsNullOrEmpty(id))
                        {
                            continue;
                        }

                        string destFile = FindDestFileById(destinationPath, id);
                        if (destFile == null)
                        {
                            Console.WriteLine($"NO MATCH: {sourcePath}");
                            mismatches++;
                            continue;
                        }

                        var sourceInfo = new FileInfo(sourcePath);
                        var destInfo = new FileInfo(destFile);

                        if (sourceInfo.Length > 0)
                        {
                            if (Math.Abs(sourceInfo.Length - destInfo.Length) > 3000)
                            {
                                Console.WriteLine($"WARNING: Size mismatch for [{id}]");
                                Console.WriteLine($"  Origin:      {sourcePath} ({sourceInfo.Length} bytes)");
                                Console.WriteLine($"  Destination: {destFile} ({destInfo.Length} bytes)");
                                mismatches++;

                                try
                                {
                                    FileSystem.DeleteFile(destFile, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                                    CopyFile(sourceInfo, Path.GetDirectoryName(destFile), overwrite: false, mtpMode: mtpMode);
                                    Console.WriteLine($"  Replaced.");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"  Error replacing: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine($"SKIP (invalid path chars): {sourcePath}");
                        mismatches++;
                    }
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"SKIP (invalid path): {originFolders[i]}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            return mismatches;
        }

        // Copies files from a source folder to a destination folder, skipping files that are already up to date.
        // Only files whose version is newer than all existing destination versions are copied.
        // In local mode (mtpMode = false), source files that are already present at the destination are sent to the Recycle Bin.
        // Returns: the number of files successfully copied.
        private static int SynchronizeFolders(
            string sourceFolder,     // path of the folder containing the source files.
            string destFolder,       // path of the folder where files should be copied.
            string expectedIdPrefix, // 12-character title ID prefix; files with a different prefix are skipped.
            bool mtpMode = false)    // when false, moves files instead of copying and removes redundant source files.
        {
            int nbSucessfullyCopied = 0;
            try
            {
                string[] files = Directory.GetFiles(sourceFolder, "*.*", System.IO.SearchOption.AllDirectories);

                foreach (string sourcePath in files)
                {
                    string relativePath = sourcePath.Substring(sourceFolder.Length + 1);
                    string destPath = Path.Combine(destFolder, relativePath);

                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    string fileId = getId(Path.GetFileName(sourcePath));
                    if (!string.IsNullOrEmpty(expectedIdPrefix) && !string.IsNullOrEmpty(fileId)
                        && !fileId.StartsWith(expectedIdPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"SKIP (ID prefix mismatch): {Path.GetFileName(sourcePath)}");
                        continue;
                    }

                    if (shouldCopy(relativePath, destPath, new FileInfo(sourcePath).Length))
                    {
                        try
                        {
                            var fileSource = new FileInfo(sourcePath);

                            if (fileSource.Length == 0)
                            {
                                continue;
                            }

                            Console.WriteLine($"{destPath}");

                            string targetFilePath = Path.Combine(destDir, fileSource.Name);
                            bool overwrite = File.Exists(targetFilePath);

                            if (overwrite)
                            {
                                FileSystem.DeleteFile(
                                    targetFilePath,
                                    UIOption.OnlyErrorDialogs,
                                    RecycleOption.SendToRecycleBin
                                );
                            }

                            CopyFile(fileSource, targetDir: destDir, overwrite: overwrite, mtpMode: mtpMode);
                            nbSucessfullyCopied++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error copying file: {ex.Message}");
                            Console.WriteLine($"{sourcePath}");
                            Console.WriteLine($"{destPath}");
                        }
                    }
                    else if (!mtpMode && !string.IsNullOrEmpty(fileId))
                    {
                        // Dest already has this file at same or newer version; remove from origin
                        try
                        {
                            FileSystem.DeleteFile(sourcePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                            Console.WriteLine($"REMOVED (already in dest): {Path.GetFileName(sourcePath)}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error removing from origin: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying files: {ex.Message}");
                throw;
            }
            return nbSucessfullyCopied;
        }

        // Moves loose files from the root of the origin path to their matching destination folder.
        // Each file is matched to a destination folder using its title ID (exact match, then prefix fallback).
        // In local mode (mtpMode = false), files already present at the destination are sent to the Recycle Bin.
        // Returns: the number of files successfully moved.
        private static int MoveOriginFilesToDestination(
            string originPath,                          // path containing the loose files to process.
            Dictionary<string, string> idToDestFolder,  // map of title IDs to the destination folder paths that contain them.
            bool mtpMode = false)                       // when false, removes source files that are already present at the destination.
        {
            int nbSuccessfullyMoved = 0;
            string[] files = Directory.GetFiles(originPath, "*.*", System.IO.SearchOption.TopDirectoryOnly);

            foreach (string sourcePath in files)
            {
                try
                {
                    string fileName = Path.GetFileName(sourcePath);
                    string fid = getId(fileName);

                    if (string.IsNullOrEmpty(fid))
                    {
                        continue;
                    }

                    if (!idToDestFolder.TryGetValue(fid, out string destFolder))
                    {
                        // Fallback: match by 12-character ID prefix (covers base↔update/DLC ID variants)
                        string prefix = GetTitlePrefix(fid);
                        var kvp = idToDestFolder.FirstOrDefault(
                            kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                        if (kvp.Key == null)
                        {
                            Console.WriteLine($"NO MATCH: {sourcePath}");
                            continue;
                        }
                        destFolder = kvp.Value;
                    }

                    var sourceInfo = new FileInfo(sourcePath);
                    if (sourceInfo.Length == 0)
                    {
                        continue;
                    }

                    string destPath = Path.Combine(destFolder, fileName);

                    if (shouldCopy(fileName, destPath, sourceInfo.Length))
                    {
                        try
                        {
                            Console.WriteLine($"MOVE -> {destPath}");

                            if (File.Exists(destPath))
                            {
                                FileSystem.DeleteFile(destPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                            }

                            File.Move(sourcePath, destPath);
                            nbSuccessfullyMoved++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error moving file: {ex.Message}");
                            Console.WriteLine($"  {sourcePath}");
                            Console.WriteLine($"  {destPath}");
                        }
                    }
                    else if (!mtpMode)
                    {
                        // Dest already has this file at same or newer version; remove from origin
                        try
                        {
                            FileSystem.DeleteFile(sourcePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                            Console.WriteLine($"REMOVED (already in dest): {fileName}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error removing from origin: {ex.Message}");
                            Console.WriteLine($"  {sourcePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing: {ex.Message}");
                    Console.WriteLine($"  {sourcePath}");
                }
            }
            return nbSuccessfullyMoved;
        }

        // Recursively searches a directory tree for the first file whose name contains the given title ID.
        // Returns: the full path of the first matching file, or null if no match is found.
        private static string FindDestFileById(
            string destRoot, // root directory to search.
            string id)       // title ID string to look for within each filename.
        {
            try
            {
                foreach (string f in Directory.EnumerateFiles(destRoot))
                {
                    if (Path.GetFileName(f).IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return f;
                    }
                }

                foreach (string dir in Directory.EnumerateDirectories(destRoot))
                {
                    string result = FindDestFileById(dir, id);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied: {destRoot} - {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine($"Directory not found: {destRoot} - {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"I/O error in {destRoot} - {ex.Message}");
            }

            return null;
        }

        // Copies or moves a file to a target directory, optionally overwriting an existing file.
        // Uses a buffered stream copy when mtpMode is true (MTP-safe), or File.Move when mtpMode is false.
        private static void CopyFile(
            FileInfo file,          // source file to copy or move.
            string targetDir,       // destination directory path.
            bool overwrite = false, // when true, deletes an existing file at the target path before writing.
            bool mtpMode = false)   // when true, uses stream copy (MTP-safe); when false, uses File.Move.
        {
            string targetFilePath = Path.Combine(targetDir, file.Name);

            if (File.Exists(targetFilePath))
            {
                if (overwrite)
                {
                    try
                    {
                        File.Delete(targetFilePath);
                    }
                    catch
                    {
                        // ignore delete errors
                    }
                }
                else
                {
                    return; // don't overwrite
                }
            }

            if (!mtpMode)
            {
                File.Move(file.FullName, targetFilePath);
            }
            else
            {
                CopyViaFileStream(file, targetFilePath);
            }
        }

        // Copies a file using a 1 MB buffered stream, which is compatible with MTP-mounted devices.
        private static void CopyViaFileStream(
            FileInfo file,         // source file to copy.
            string targetFilePath) // full destination path where the file will be written.
        {
            using (var sourceStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
            using (var targetStream = new FileStream(targetFilePath, FileMode.CreateNew))
            {
                byte[] buffer = new byte[1024 * 1024];
                int bytesRead;

                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    targetStream.Write(buffer, 0, bytesRead);
                }
            }
        }
    }
}
