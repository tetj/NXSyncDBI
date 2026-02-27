using MediaDevices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CopyUpdates
{
    partial class Program
    {
        // Uploads local game files to an MTP device for games that are already installed on the Switch.
        // Only updates and DLCs are uploaded; base games are always skipped.
        // Files already present on the device at the same or a newer version are also skipped.
        private static void UploadToMtp(
            string localOriginPath, // local folder containing the game files to upload.
            string mtpDestPath,     // path on the MTP device where uploaded files will be placed.
            string mtpRefPath,      // path on the MTP device scanned to detect which games are installed.
            string deviceName,      // optional substring of the device friendly name; uses the first device if empty.
            bool uploadAll = false) // when true, base games are also uploaded if not already installed.
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
                HashSet<string> installedPrefixes;
                Dictionary<string, int> switchContentMap;


                bool flowControl = ScanMTP(mtpRefPath, device, out installedPrefixes, out switchContentMap);
                if (!flowControl)
                {
                    return;
                }

                UploadMissingFiles(localOriginPath, mtpDestPath, device, installedPrefixes, switchContentMap, uploadAll);
            }
            finally
            {
                device.Disconnect();
            }
        }

        // Step 1: Scan the MTP device to detect which games are installed and which versions they have
        private static bool ScanMTP(string mtpRefPath, MediaDevice device, out HashSet<string> installedPrefixes, out Dictionary<string, int> switchContentMap)
        {
            // Step 1: Scan installed games on the Switch
            Console.WriteLine($"Scanning installed games at {mtpRefPath}...");

            // 12-character title ID prefixes of every game installed on the Switch,
            // used to quickly decide whether to upload any file for a given game.                
            installedPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // maps each exact title ID to the highest version currently installed,
            // used to skip uploads when the Switch already has the same or a newer version.
            switchContentMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // DBI MTP mode exposes installed games as named directories (e.g. "Game [01004D300C5C6000] [v0]")
                // rather than as NSP files. Extract title IDs from directory names first, then also from
                // any files found inside, to cover both DBI-style and NSP-file-based organizations.
                foreach (string mtpGameFolder in TryEnumerateMtpDirectories(device, mtpRefPath) ?? new List<string>())
                {
                    try
                    {
                        // The folder name itself often contains the bracketed title ID in DBI MTP mode.
                        string folderName = Path.GetFileName(mtpGameFolder);
                        string folderFid = getId(folderName);
                        if (!string.IsNullOrEmpty(folderFid))
                        {
                            installedPrefixes.Add(GetTitlePrefix(folderFid));
                            int folderVer = getVersion(folderName);
                            if (!switchContentMap.TryGetValue(folderFid, out int existingVer) || folderVer > existingVer)
                            {
                                switchContentMap[folderFid] = folderVer;
                            }
                        }

                        // Also scan files directly inside the directory for NSP-style naming.
                        foreach (string mtpFile in TryEnumerateMtpFiles(device, mtpGameFolder) ?? new List<string>())
                        {
                            string fid = getId(Path.GetFileName(mtpFile));
                            if (string.IsNullOrEmpty(fid))
                            {
                                continue;
                            }
                            installedPrefixes.Add(GetTitlePrefix(fid));
                            int ver = getVersion(Path.GetFileName(mtpFile));
                            if (!switchContentMap.TryGetValue(fid, out int existingVer) || ver > existingVer)
                            {
                                switchContentMap[fid] = ver;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scanning {mtpGameFolder}: {ex.Message}");
                    }
                }

                // Also scan files sitting directly at the root of mtpRefPath.
                foreach (string mtpFile in TryEnumerateMtpFiles(device, mtpRefPath) ?? new List<string>())
                {
                    try
                    {
                        string fid = getId(Path.GetFileName(mtpFile));
                        if (string.IsNullOrEmpty(fid))
                        {
                            continue;
                        }
                        installedPrefixes.Add(GetTitlePrefix(fid));
                        int ver = getVersion(Path.GetFileName(mtpFile));
                        if (!switchContentMap.TryGetValue(fid, out int existingVer) || ver > existingVer)
                        {
                            switchContentMap[fid] = ver;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scanning {mtpFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning '{mtpRefPath}': {ex.Message}");
                return false;
            }
            Console.WriteLine($"{installedPrefixes.Count} game(s) found on Switch.");
            return true;
        }

        // Step 2: Check local backup and upload what is missing or outdated.
        // Scans all files recursively under localOriginPath regardless of how they are organised into subfolders,
        // because the local folder layout is user-defined and can nest games arbitrarily deep.
        // Per-file filtering (base-game skip, installed-prefix check, version comparison) is handled by UploadFileIfNeeded.
        private static void UploadMissingFiles(
            string localOriginPath,
            string mtpDestPath,
            MediaDevice device,
            HashSet<string> installedPrefixes,
            Dictionary<string, int> switchContentMap,
            bool uploadAll = false) // when true, base games are also uploaded if not already installed.
        {
            int nbUploaded = 0;

            foreach (string localFile in Directory.GetFiles(localOriginPath, "*.*", System.IO.SearchOption.AllDirectories))
            {
                Console.WriteLine($"\nProcessing file: {localFile}");
                if (UploadFileIfNeeded(device, localFile, mtpDestPath, installedPrefixes, switchContentMap, uploadAll))
                {
                    nbUploaded++;
                }
            }

            Console.WriteLine($"\n{nbUploaded} file(s) uploaded to {mtpDestPath}.");
        }


    }
}
