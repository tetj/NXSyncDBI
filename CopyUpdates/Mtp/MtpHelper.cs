using MediaDevices;
using Microsoft.VisualBasic.FileIO;

namespace CopyUpdates
{
    partial class Program
    {
        // Writes a message to the console in red, then resets the color.
        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        // Writes a message to the console in yellow, then resets the color.
        private static void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        // Uploads a single local file to the specified directory on the MTP device.
        // Retries up to maxRetries times on failure, waiting retryDelaySeconds between attempts.
        // Returns: true if the upload succeeded; false if all attempts failed.
        public static bool UploadToMtp(
            MediaDevice device,        // the connected MTP device.
            string localFilePath,      // full local path of the file to upload.
            string mtpDestDir,         // destination directory path on the MTP device.
            string fileName,           // name the file will have on the device.
            int maxRetries = 0,        // number of additional attempts after the first failure.
            int retryDelaySeconds = 5) // seconds to wait between retries.
        {
            string mtpFilePath = mtpDestDir.TrimEnd('\\') + "\\" + fileName;
            Console.WriteLine($"UPLOAD -> {mtpFilePath}");

            for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
            {
                try
                {
                    device.UploadFile(localFilePath, mtpFilePath);
                    return true;
                }
                catch (Exception ex)
                {
                    bool isLastAttempt = attempt > maxRetries;
                    if (isLastAttempt)
                    {
                        WriteError($"Error uploading {fileName} (attempt {attempt}/{maxRetries + 1}): {ex.Message}");
                        return false;
                    }

                    WriteWarning($"Upload failed (attempt {attempt}/{maxRetries + 1}), retrying in {retryDelaySeconds}s... ({ex.Message})");
                    System.Threading.Thread.Sleep(retryDelaySeconds * 1000);
                }
            }

            return false;
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
            MediaDevice device,
            string localFile,
            string mtpDestPath,
            HashSet<string> installedPrefixes,
            Dictionary<string, int> switchContentMap,
            bool uploadAll = false)
        {
            var fi = new FileInfo(localFile);
            if (fi.Length == 0)
            {
                if (Verbose)
                {
                    Console.WriteLine($"  SKIP (empty file): {Path.GetFileName(localFile)}");
                }
                return false;
            }

            string fileName = Path.GetFileName(localFile);
            string fid = getId(fileName);

            if (string.IsNullOrEmpty(fid))
            {
                if (Verbose)
                {
                    Console.WriteLine($"  SKIP (no title ID): {fileName}");
                }
                return false;
            }

            if (IsBaseGame(fid))
            {
                if (!uploadAll)
                {
                    if (Verbose)
                    {
                        Console.WriteLine($"  SKIP (base game, use -all to include): {fileName}");
                    }
                    return false;
                }

                if (switchContentMap.ContainsKey(fid))
                {
                    if (Verbose)
                    {
                        Console.WriteLine($"  SKIP (base game already installed): {fileName}");
                    }
                    return false;
                }

                return UploadToMtp(device, localFile, mtpDestPath, fileName);
            }

            string prefix = GetTitlePrefix(fid);
            if (!installedPrefixes.Contains(prefix))
            {
                if (Verbose)
                {
                    Console.WriteLine($"  SKIP (game not installed on Switch, prefix={prefix}): {fileName}");
                }
                return false;
            }

            int localVer = getVersion(fileName);
            if (switchContentMap.TryGetValue(fid, out int switchVer) && switchVer >= localVer)
            {
                if (Verbose)
                {
                    Console.WriteLine($"  SKIP (Switch has v{switchVer} >= local v{localVer}): {fileName}");
                }
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
        public static List<string>? TryEnumerateMtpFiles(
            MediaDevice device,      // the connected MTP device.
            string path,             // the MTP path to list files from.
            int timeoutSeconds = 30) // maximum seconds to wait before giving up (default: 30).
        {
            var task = Task.Run(() => device.EnumerateFiles(path).ToList());
            if (!task.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                WriteWarning($"\nTimeout ({timeoutSeconds}s) listing files in: {path} — skipping.");
                return null;
            }
            if (task.IsFaulted)
            {
                throw task.Exception!.InnerException!;
            }
            return task.Result;
        }

        // Lists all subdirectories at the given MTP path, enforcing a timeout to handle unresponsive devices.
        // Returns: a list of directory paths, or null if the operation timed out.
        public static List<string>? TryEnumerateMtpDirectories(
            MediaDevice device,      // the connected MTP device.
            string path,             // the MTP path to list directories from.
            int timeoutSeconds = 30) // maximum seconds to wait before giving up (default: 30).
        {
            var task = Task.Run(() => device.EnumerateDirectories(path).ToList());
            if (!task.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                WriteWarning($"\nTimeout ({timeoutSeconds}s) listing directories in: {path} — skipping.");
                return null;
            }
            if (task.IsFaulted)
            {
                throw task.Exception!.InnerException!;
            }
            return task.Result;
        }

        // Downloads a single file from the MTP device
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
                WriteError($"Error downloading {fileName}: {ex.Message}");
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
