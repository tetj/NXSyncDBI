using MediaDevices;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CopyUpdates
{
    partial class Program
    {
        // Uploads a single local file to the specified directory on the MTP device.
        // Returns: true if the upload succeeded; false if an error occurred.
        public static bool UploadToMtp(
            MediaDevice device,   // the connected MTP device.
            string localFilePath, // full local path of the file to upload.
            string mtpDestDir,    // destination directory path on the MTP device.
            string fileName)      // name the file will have on the device.
        {
            string mtpFilePath = mtpDestDir.TrimEnd('\\') + "\\" + fileName;
            try
            {
                Console.WriteLine($"UPLOAD -> {mtpFilePath}");
                device.UploadFile(localFilePath, mtpFilePath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading {fileName}: {ex.Message}");
                return false;
            }
        }

        // Checks whether a local file needs to be uploaded to the Switch and uploads it if so.
        // In normal mode a file is uploaded only when all of the following are true:
        //   - The file is not empty
        //   - The file has a recognizable title ID
        //   - The file is an update or DLC (base games are skipped)
        //   - The game is currently installed on the Switch
        //   - The Switch does not already have the same version or a newer one
        // In -all mode, base games are also uploaded, but only when they are not already installed.
        // Returns: true if the file was uploaded successfully; false if it was skipped or an error occurred.
        public static bool UploadFileIfNeeded(
            MediaDevice device,                       // the connected MTP device.
            string localFile,                         // full local path of the file to potentially upload.
            string mtpDestPath,                       // destination directory path on the MTP device.
            HashSet<string> installedPrefixes,        // set of 12-character title ID prefixes for games installed on the Switch.
            Dictionary<string, int> switchContentMap, // map of exact title IDs to the highest version currently installed on the Switch.
            bool uploadAll = false)                   // when true, base games are also uploaded if not already installed.
        {
            var fi = new FileInfo(localFile);
            if (fi.Length == 0)
            {
                return false;
            }

            string fileName = Path.GetFileName(localFile);
            string fid = getId(fileName);

            if (string.IsNullOrEmpty(fid))
            {
                return false;
            }

            // In normal mode, skip base games entirely.
            // In -all mode, upload the base game only if it is not already installed on the Switch.
            if (IsBaseGame(fid))
            {
                if (!uploadAll)
                {
                    return false;
                }

                if (switchContentMap.ContainsKey(fid))
                {
                    return false;
                }

                return UploadToMtp(device, localFile, mtpDestPath, fileName);
            }

            // Update / DLC: skip if the game is not installed on the Switch
            string prefix = GetTitlePrefix(fid);
            if (!installedPrefixes.Contains(prefix))
            {
                return false;
            }

            // Skip if the Switch already has this version or a newer one
            int localVer = getVersion(fileName);
            if (switchContentMap.TryGetValue(fid, out int switchVer) && switchVer >= localVer)
            {
                return false;
            }

            return UploadToMtp(device, localFile, mtpDestPath, fileName);
        }


        // Recursively enumerates all files within an MTP path, including files in subdirectories.
        // Returns: an enumerable sequence of full MTP file paths.
        public static IEnumerable<string> GetMtpFilesRecursive(
            MediaDevice device, // the connected MTP device.
            string mtpPath)     // the MTP path to enumerate.
        {
            foreach (string file in TryEnumerateMtpFiles(device, mtpPath) ?? new List<string>())
            {
                yield return file;
            }

            foreach (string dir in TryEnumerateMtpDirectories(device, mtpPath) ?? new List<string>())
            {
                foreach (string file in GetMtpFilesRecursive(device, dir))
                {
                    yield return file;
                }
            }
        }

        // Lists all files at the given MTP path, enforcing a timeout to handle unresponsive devices.
        // Returns: a list of file paths, or null if the operation timed out.
        public static List<string> TryEnumerateMtpFiles(
            MediaDevice device,      // the connected MTP device.
            string path,             // the MTP path to list files from.
            int timeoutSeconds = 30) // maximum seconds to wait before giving up (default: 30).
        {
            var task = Task.Run(() => device.EnumerateFiles(path).ToList());
            if (!task.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                Console.WriteLine($"\nTimeout ({timeoutSeconds}s) listing files in: {path} — skipping.");
                return null;
            }
            if (task.IsFaulted)
            {
                throw task.Exception.InnerException;
            }
            return task.Result;
        }

        // Lists all subdirectories at the given MTP path, enforcing a timeout to handle unresponsive devices.
        // Returns: a list of directory paths, or null if the operation timed out.
        public static List<string> TryEnumerateMtpDirectories(
            MediaDevice device,      // the connected MTP device.
            string path,             // the MTP path to list directories from.
            int timeoutSeconds = 30) // maximum seconds to wait before giving up (default: 30).
        {
            var task = Task.Run(() => device.EnumerateDirectories(path).ToList());
            if (!task.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                Console.WriteLine($"\nTimeout ({timeoutSeconds}s) listing directories in: {path} — skipping.");
                return null;
            }
            if (task.IsFaulted)
            {
                throw task.Exception.InnerException;
            }
            return task.Result;
        }

        // Downloads a single file from the MTP device to the local file system.
        // If a file already exists at the destination, it is sent to the Recycle Bin before downloading.
        // If the download fails, any partially written local file is deleted.
        // Returns: true if the download succeeded; false if an error occurred.
        public static bool DownloadFromMtp(
            MediaDevice device,  // the connected MTP device.
            string mtpFilePath,  // full path of the file on the MTP device.
            string localDestDir, // local directory where the file will be saved.
            string fileName)     // name the file will have locally.
        {
            string localDestPath = Path.Combine(localDestDir, fileName);
            try
            {
                Console.WriteLine($"{localDestPath}");

                if (File.Exists(localDestPath))
                {
                    FileSystem.DeleteFile(localDestPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }

                using (var targetStream = new FileStream(localDestPath, FileMode.CreateNew, FileAccess.Write))
                {
                    device.DownloadFile(mtpFilePath, targetStream);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading {fileName}: {ex.Message}");
                try
                {
                    if (File.Exists(localDestPath))
                    {
                        File.Delete(localDestPath);
                    }
                }
                catch
                {
                }
                return false;
            }
        }
    }
}
