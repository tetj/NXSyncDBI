using Microsoft.VisualBasic.FileIO;
using System;
using System.IO;
using System.Linq;

namespace CopyUpdates
{
    partial class Program
    {
        // Extracts the bracketed ID from a filename, e.g. "[0100XXXXXXXX0000]" → "0100XXXXXXXX0000".
        // Returns: the content inside the first pair of brackets, or null if none is found.
        private static string getId(
            string s) // the filename string to search within.
        {
            int start = s.IndexOf('[');
            int end = s.IndexOf(']', start + 1);

            if (start >= 0 && end > start)
            {
                return s.Substring(start + 1, end - start - 1);
            }
            return null;
        }

        // Extracts the version number from a "[vN]" tag embedded in a filename, e.g. "[v65536]" → 65536.
        // Returns: the integer version number, or 0 if no valid version tag is found.
        private static int getVersion(
            string s) // the filename string to search within.
        {
            int start = s.IndexOf("[v", StringComparison.Ordinal);
            if (start >= 0)
            {
                start += 2; // skip "[v"
                int end = s.IndexOf(']', start);

                if (end > start)
                {
                    string number = s.Substring(start, end - start);
                    if (int.TryParse(number, out int value))
                    {
                        return value;
                    }
                }
            }
            return 0;
        }

        // Determines whether a title ID belongs to a base game rather than an update or DLC.
        // Nintendo Switch base game IDs are exactly 16 hex characters and end with "0000".
        // Returns: true if the title ID represents a base game; false otherwise.
        private static bool IsBaseGame(
            string titleId) // the 16-character hexadecimal title ID to check.
        {
            // Nintendo Switch base game IDs are 16 hex chars ending in 0000
            // Updates end in 0800, DLCs end in 0001-07FF
            return titleId != null && titleId.Length == 16
                && titleId.EndsWith("0000", StringComparison.OrdinalIgnoreCase);
        }

        // Returns the first 12 characters of a title ID.
        // This prefix is shared between a base game, its updates, and its DLCs,
        // and is used to group or match related content across different ID variants.
        // Returns: the first 12 characters of titleId, or the full string if it is shorter than 12 characters.
        private static string GetTitlePrefix(
            string titleId) // the title ID string to truncate.
        {
            if (titleId != null && titleId.Length >= 12)
            {
                return titleId.Substring(0, 12);
            }
            return titleId;
        }

        // Decides whether a source file should be copied to the destination based on version and size.
        // A copy is needed when no file with the same ID exists in the destination directory,
        // or when the source version is higher than all existing versions (older versions are recycled first).
        // Returns: true if the file should be copied; false if the destination is already up to date.
        private static bool shouldCopy(
            string relativePath,             // filename or relative path used to extract the ID and version.
            string destPath,                 // full path of the intended destination file.
            long sourceLength,               // size of the source file in bytes.
            bool skipSizeComparison = false) // when true, same-version files are not compared by size (used for MTP sources).
        {
            string destDir = Path.GetDirectoryName(destPath);
            int version = getVersion(relativePath);

            // adding this because DBI changed the filename format so I must cover all formats, so better to check the ID
            string id = getId(relativePath);

            // Decide whether to copy:
            // - If no file with the same id exists in the destination directory -> copy
            // - If one or more files with the same id exist, only copy when the source version is greater
            //   than all existing versions. In that case remove older versions before copying.
            bool result = false;

            if (!string.IsNullOrEmpty(id))
            {
                var existingFiles = Directory.EnumerateFiles(destDir)
                    .Where(f => Path.GetFileName(f).IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                if (!existingFiles.Any())
                {
                    result = true;
                }
                else
                {
                    int maxExistingVersion = existingFiles
                        .Select(f => getVersion(Path.GetFileName(f)))
                        .DefaultIfEmpty(0)
                        .Max();

                    if (version > maxExistingVersion)
                    {
                        // delete older versions that have lower version number
                        foreach (var ef in existingFiles)
                        {
                            try
                            {
                                int ev = getVersion(Path.GetFileName(ef));
                                if (ev < version)
                                {
                                    FileSystem.DeleteFile(
                                        ef,
                                        UIOption.OnlyErrorDialogs,
                                        RecycleOption.SendToRecycleBin
                                    );
                                }
                            }
                            catch
                            {
                                // ignore delete errors and continue
                            }
                        }
                        result = true;
                    }

                    if (version == maxExistingVersion && !skipSizeComparison)
                    {
                        var matchingFile = existingFiles.FirstOrDefault(f => getVersion(Path.GetFileName(f)) == maxExistingVersion);
                        if (matchingFile != null)
                        {
                            long destSize = new FileInfo(matchingFile).Length;
                            result = sourceLength > destSize;
                        }
                        // skipSizeComparison=true (MTP): same version already present → skip
                    }
                }
            }
            else
            {
                // If we can't determine an id, fall back to file existence check
                result = !File.Exists(destPath);
            }
            return result;
        }
    }
}
