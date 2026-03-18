using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopyUpdates
{
    internal class FileHelper
    {
        // Move the folder to the target directory
        // E.g. MoveFolder("C:\\Games\\Game1", "C:\\Sorted", "Game1") will move the folder to "C:\\Sorted\\Game1"
        public static void MoveFolder(string sourceFolder, string targetDir, string gameName)
        {
            string targetPath = Path.Combine(targetDir, gameName);

            // Handle case where target folder already exists
            if (Directory.Exists(targetPath))
            {
                int counter = 1;
                string newName = gameName;
                do
                {
                    newName = $"{gameName} ({counter++})";
                    targetPath = Path.Combine(targetDir, newName);
                } while (Directory.Exists(targetPath));
            }

            Directory.Move(sourceFolder, targetPath);
        }

        public static bool IsRatingDirectory(string path)
        {
            string dirName = Path.GetFileName(path);
            return dirName == "Everyone" ||
                   dirName == "Everyone 10+" ||
                   dirName == "Teen" ||
                   dirName == "Mature" ||
                   dirName == "Unknown Rating" ||
                   dirName == "Games Not Found";
        }

        public static string GetTargetDirectory(Dictionary<string, string> ratingDirs, AgeRating rating)
        {
            string key = (rating.category, rating.rating) switch
            {
                // ESRB Ratings mapped to consolidated categories
                (1, 7) => "Everyone",     // EC -> Everyone
                (1, 8) => "Everyone",     // E -> Everyone
                (1, 9) => "Everyone 10+", // E10+ -> Everyone 10+
                (1, 10) => "Teen",        // T -> Teen
                (1, 11) => "Mature",      // M -> Mature
                (1, 12) => "Mature",      // AO -> Mature

                // PEGI Ratings mapped to consolidated categories
                (2, 1) => "Everyone",     // PEGI 3+ -> Everyone
                (2, 2) => "Everyone",     // PEGI 7+ -> Everyone
                (2, 3) => "Everyone 10+", // PEGI 12+ -> Everyone 10+
                (2, 4) => "Teen",         // PEGI 16+ -> Teen
                (2, 5) => "Mature",       // PEGI 18+ -> Mature

                (5, 23) => "Everyone",
                (5, 24) => "Teen",
                (5, 25) => "Teen",
                (5, 26) => "Mature",

                (4, 18) => "Everyone",
                (4, 19) => "Everyone",
                (4, 20) => "Teen",
                (4, 21) => "Teen",
                (4, 22) => "Mature",

                (3, 13) => "Everyone",
                (3, 14) => "Teen",
                (3, 15) => "Teen",
                (3, 16) => "Mature",
                (3, 17) => "Mature",

                // Rating Pending and Unknown cases
                (1, 6) => "Unknown",
                _ => "Unknown"
            };

            return ratingDirs.ContainsKey(key) ? ratingDirs[key] : ratingDirs["Unknown"];
        }

        public static string FormatAgeRating(List<AgeRating> ratings)
        {
            var result = new StringBuilder();
            foreach (var rating in ratings)
            {
                string ratingSystem = rating.category switch
                {
                    1 => "ESRB",
                    2 => "PEGI",
                    _ => "Unknown"
                };

                string ratingValue = (rating.category, rating.rating) switch
                {
                    (1, 6) => "RP (Rating Pending)",
                    (1, 7) => "EC (Early Childhood)",
                    (1, 8) => "E (Everyone)",
                    (1, 9) => "E10+ (Everyone 10+)",
                    (1, 10) => "T (Teen)",
                    (1, 11) => "M (Mature)",
                    (1, 12) => "AO (Adults Only)",
                    (2, 1) => "3+",
                    (2, 2) => "7+",
                    (2, 3) => "12+",
                    (2, 4) => "16+",
                    (2, 5) => "18+",
                    _ => $"Unknown ({rating.rating})"
                };

                string consolidatedCategory = (rating.category, rating.rating) switch
                {
                    (1, 7) or (1, 8) or (2, 1) or (2, 2) => "Everyone",
                    (1, 9) or (2, 3) => "Everyone 10+",
                    (1, 10) or (2, 4) => "Teen",
                    (1, 11) or (1, 12) or (2, 5) => "Mature",
                    _ => "Unknown"
                };

                result.AppendLine($"{ratingSystem}: {ratingValue} (Consolidated: {consolidatedCategory})");
            }

            return result.ToString().TrimEnd();
        }

        public static Dictionary<string, string> CreateRatingDirectories(string basePath)
        {
            var dirs = new Dictionary<string, string>
            {
                { "Everyone",    Path.Combine(basePath, "Everyone") },
                { "Everyone10Plus", Path.Combine(basePath, "Everyone 10+") },
                { "Teen",        Path.Combine(basePath, "Teen") },
                { "Mature",      Path.Combine(basePath, "Mature") },
                { "Unknown",     Path.Combine(basePath, "Unknown Rating") },
                { "NotFound",    Path.Combine(basePath, "Games Not Found") }
            };

            foreach (var dir in dirs.Values)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            return dirs;
        }
    }
}
