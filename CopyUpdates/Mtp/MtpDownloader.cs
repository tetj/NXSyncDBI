using MediaDevices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopyUpdates
{
    partial class Program
    {
        // Downloads game files from an MTP device to the local destination folder.
        // Each game folder on the device is matched to a local folder by shared file ID.
        // Only files that are missing locally or have a newer version on the device are downloaded.
        private static void DownloadFromMtp(
            string mtpOriginPath,   // path on the MTP device to scan for game folders.
            string destinationPath, // local root path where game files are organized by folder.
            string deviceName)      // optional substring of the device friendly name; uses the first device if empty.
        {
            var allDevices = MediaDevice.GetDevices().ToList();
            if (allDevices.Count == 0)
            {
                Console.WriteLine("No MTP devices found. Make sure your Switch is connected and DBI MTP mode is active.");
                return;
            }

            MediaDevice device;
            if (string.IsNullOrEmpty(deviceName))
            {
                device = allDevices.First();
            }
            else
            {
                device = allDevices.FirstOrDefault(d => d.FriendlyName.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (device == null)
            {
                Console.WriteLine($"Device '{deviceName}' not found. Connected devices:");
                foreach (var d in allDevices)
                {
                    Console.WriteLine($"  - {d.FriendlyName}");
                }
                return;
            }

            device.Connect();
            Console.WriteLine($"Connected to: {device.FriendlyName}");

            try
            {
                // Build idToDestFolder map from local destination
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

                // Enumerate game folders on the device
                List<string> mtpFolders;
                try
                {
                    mtpFolders = TryEnumerateMtpDirectories(device, mtpOriginPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error listing '{mtpOriginPath}' on device: {ex.Message}");
                    return;
                }
                if (mtpFolders == null)
                {
                    return;
                }

                int total = mtpFolders.Count;
                if (total == 0)
                {
                    Console.WriteLine($"No subfolders found at {mtpOriginPath}.");
                }

                // For each game folder on the MTP device, find the matching local destination folder
                // by comparing file IDs. First try an exact ID match, then fall back to a prefix match
                // to handle cases where base game, update, and DLC share the same 12-character prefix.
                for (int i = 0; i < total; i++)
                {
                    string mtpFolder = mtpFolders[i];
                    DrawProgressBar(i + 1, total);

                    string matchedDestFolder = null;
                    string matchedFid = null;

                    try
                    {
                        foreach (string mtpFile in TryEnumerateMtpFiles(device, mtpFolder) ?? new List<string>())
                        {
                            string fid = getId(Path.GetFileName(mtpFile));
                            if (string.IsNullOrEmpty(fid))
                            {
                                continue;
                            }

                            if (idToDestFolder.TryGetValue(fid, out string dest))
                            {
                                matchedDestFolder = dest;
                                matchedFid = fid;
                                Console.WriteLine("\nMatch found for " + mtpFolder);
                                break;
                            }

                            string prefix = GetTitlePrefix(fid);
                            var kvp = idToDestFolder.FirstOrDefault(
                                kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                            if (kvp.Key != null)
                            {
                                matchedDestFolder = kvp.Value;
                                matchedFid = fid;
                                Console.WriteLine("\nMatch found (prefix) for " + mtpFolder);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // The semaphore timeout period has expired.  (Exception from HRESULT: 0x80070079)
                        Console.WriteLine($"Error scanning {mtpFolder}: {ex.Message}");
                        continue;
                    }

                    if (matchedDestFolder != null)
                    {
                        string expectedPrefix = GetTitlePrefix(matchedFid);
                        DownloadOneGameFromMtp(device, mtpFolder, matchedDestFolder, expectedPrefix);
                    }
                }

                // Process loose files at the root of the MTP origin path
                try
                {
                    foreach (string mtpFile in TryEnumerateMtpFiles(device, mtpOriginPath) ?? new List<string>())
                    {
                        string fileName = Path.GetFileName(mtpFile);
                        string fid = getId(fileName);
                        if (string.IsNullOrEmpty(fid))
                        {
                            continue;
                        }

                        if (!idToDestFolder.TryGetValue(fid, out string destFolder))
                        {
                            string prefix = GetTitlePrefix(fid);
                            var kvp = idToDestFolder.FirstOrDefault(
                                kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                            if (kvp.Key == null)
                            {
                                Console.WriteLine($"NO MATCH: {mtpFile}");
                                continue;
                            }
                            destFolder = kvp.Value;
                        }

                        try
                        {
                            long fileSize = (long)device.GetFileInfo(mtpFile).Length;
                            if (fileSize == 0)
                            {
                                continue;
                            }
                            string destPath = Path.Combine(destFolder, fileName);
                            if (shouldCopy(fileName, destPath, fileSize, skipSizeComparison: true))
                            {
                                DownloadFromMtp(device, mtpFile, destFolder, fileName);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing {fileName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error listing root MTP files: {ex.Message}");
                }
            }
            finally
            {
                device.Disconnect();
            }
        }

        // Downloads all files from an MTP game folder to the local destination folder.
        // Preserves subfolder structure relative to the MTP game folder root.
        // Skips files whose title ID prefix does not match the expected prefix for this game.
        // Returns: the number of files successfully downloaded.
        private static int DownloadOneGameFromMtp(
            MediaDevice device,      // the connected MTP device.
            string mtpFolder,        // path of the game folder on the MTP device.
            string destFolder,       // local destination folder where files will be saved.
            string expectedIdPrefix) // 12-character title ID prefix used to filter out unrelated files.
        {
            int nbCopied = 0;
            try
            {
                foreach (string mtpFile in GetMtpFilesRecursive(device, mtpFolder))
                {
                    string fileName = Path.GetFileName(mtpFile);
                    string fileId = getId(fileName);

                    if (!string.IsNullOrEmpty(expectedIdPrefix) && !string.IsNullOrEmpty(fileId)
                        && !fileId.StartsWith(expectedIdPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"SKIP (ID prefix mismatch): {fileName}");
                        continue;
                    }

                    // Preserve subfolder structure relative to the MTP game folder
                    string relativePath = mtpFile.Substring(mtpFolder.Length).TrimStart('\\', '/');
                    string destPath = Path.Combine(destFolder, relativePath);
                    string destDir = Path.GetDirectoryName(destPath);

                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    long fileSize;
                    try
                    {
                        fileSize = (long)device.GetFileInfo(mtpFile).Length;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error getting info for {fileName}: {ex.Message}");
                        continue;
                    }

                    if (fileSize == 0)
                    {
                        continue;
                    }

                    if (shouldCopy(fileName, destPath, fileSize, skipSizeComparison: true))
                    {
                        if (DownloadFromMtp(device, mtpFile, destDir, fileName))
                        {
                            nbCopied++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing MTP folder {mtpFolder}: {ex.Message}");
            }
            return nbCopied;
        }

    }
}
