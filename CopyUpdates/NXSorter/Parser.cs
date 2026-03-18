using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopyUpdates
{
    public class Game
    {
        [JsonPropertyName("bannerUrl")]
        public string? BannerUrl { get; set; }

        [JsonPropertyName("category")]
        public List<string>? Categories { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("developer")]
        public string? Developer { get; set; }

        [JsonPropertyName("frontBoxArt")]
        public string? FrontBoxArt { get; set; }

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("intro")]
        public string? Intro { get; set; }

        [JsonPropertyName("isDemo")]
        public bool IsDemo { get; set; }

        [JsonPropertyName("languages")]
        public List<string> Languages { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("nsuId")]
        public long NsuId { get; set; }

        [JsonPropertyName("numberOfPlayers")]
        public int? NumberOfPlayers { get; set; }

        [JsonPropertyName("publisher")]
        public string? Publisher { get; set; }

        [JsonPropertyName("rating")]
        public int? Rating { get; set; }

        [JsonPropertyName("ratingContent")]
        public List<string>? RatingContent { get; set; }

        [JsonPropertyName("releaseDate")]
        public int? ReleaseDate { get; set; }

        [JsonPropertyName("rightsId")]
        public string? RightsId { get; set; }

        [JsonPropertyName("screenshots")]
        public List<string>? Screenshots { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }
    }

    class TitleDbParser
    {
        // E.g.: file = US.en.json
        public static List<Game> Parse(string jsonFile)
        {
            string jsonData = File.ReadAllText(jsonFile);

            try
            {
                var jsonGames = JsonSerializer.Deserialize<Dictionary<string, Game>>(jsonData);

                if (jsonGames != null)
                {
                    return new List<Game>(jsonGames.Values);
                }
                else
                {
                    Debug.WriteLine("No games found in the JSON data.");
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Error parsing JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred: {ex.Message}");
            }

            return new List<Game>();
        }
    }
}
