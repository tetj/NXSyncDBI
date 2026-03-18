using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;

namespace CopyUpdates
{
    class NSP
    {
        /// <summary>
        /// Extracts the Base Title ID from a DLC NSP filename.
        /// </summary>
        public static ulong GetBaseTitleId(string nspPath)
        {
            if (string.IsNullOrEmpty(nspPath))
                return 0UL;

            string fileName = Path.GetFileName(nspPath) ?? string.Empty;

            var matches = Regex.Matches(fileName, "\\[(.*?)\\]");

            string? hexToken = null;
            foreach (Match m in matches)
            {
                if (m.Groups.Count < 2)
                    continue;

                var token = m.Groups[1].Value;
                if (Regex.IsMatch(token, "^[0-9A-Fa-f]{12,16}$"))
                {
                    hexToken = token;
                    break;
                }
            }

            if (hexToken == null)
            {
                var fallback = Regex.Match(fileName, "[0-9A-Fa-f]{12,16}");
                if (fallback.Success)
                    hexToken = fallback.Value;
            }

            if (string.IsNullOrEmpty(hexToken))
                return 0UL;

            string baseHex = hexToken.Length >= 12 ? hexToken.Substring(0, 12) : hexToken;

            try
            {
                return ulong.Parse(baseHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0UL;
            }
        }

        /// <summary>
        /// Find a file under titlesRoot that contains the base id hex string and move the file into that folder.
        /// </summary>
        public static bool MoveToMatchingTitleFolder(string dlcPath, string titlesRoot = @"T:\BACKUPS\NintendoSwitch 554GB\_NEW\titles\")
        {
            if (string.IsNullOrEmpty(dlcPath) || !File.Exists(dlcPath))
                return false;

            ulong baseId = GetBaseTitleId(dlcPath);
            if (baseId == 0UL)
            {
                Debug.WriteLine($"Could not extract base id from '{dlcPath}'");
                return false;
            }

            string baseHex = baseId.ToString("X").PadLeft(12, '0');

            if (string.IsNullOrEmpty(titlesRoot) || !Directory.Exists(titlesRoot))
            {
                Debug.WriteLine($"Titles root not found: {titlesRoot}");
                return false;
            }

            try
            {
                var files = Directory.EnumerateFiles(titlesRoot, "*", SearchOption.AllDirectories);
                string? match = files.FirstOrDefault(f => Path.GetFileName(f).IndexOf(baseHex, StringComparison.OrdinalIgnoreCase) >= 0);

                if (match == null)
                {
                    Debug.WriteLine($"No matching title file containing '{baseHex}' found under {titlesRoot}");
                    return false;
                }

                string destDir = Path.GetDirectoryName(match) ?? titlesRoot;
                string destFileName = Path.GetFileName(dlcPath);
                string destPath = Path.Combine(destDir, destFileName);

                if (File.Exists(destPath))
                {
                    int counter = 1;
                    string nameOnly = Path.GetFileNameWithoutExtension(destFileName);
                    string ext = Path.GetExtension(destFileName);
                    string newPath;
                    do
                    {
                        newPath = Path.Combine(destDir, $"{nameOnly} ({counter++}){ext}");
                    } while (File.Exists(newPath));

                    destPath = newPath;
                }

                File.Move(dlcPath, destPath);
                Debug.WriteLine($"Moved '{dlcPath}' -> '{destPath}'");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while moving: {ex.Message}");
                return false;
            }
        }
    }
}
