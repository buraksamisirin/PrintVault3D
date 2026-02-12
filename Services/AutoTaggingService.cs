using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PrintVault3D.Services;

/// <summary>
/// Service for automatic tag generation based on file names and keywords from JSON configuration.
/// </summary>
public class AutoTaggingService : IAutoTaggingService
{
    private readonly ILogger<AutoTaggingService>? _logger;
    private readonly Dictionary<string, string> _keywordMappings;
    private readonly Dictionary<string, string> _categoryMappings;
    private readonly HashSet<string> _stopWords;

    public AutoTaggingService(ILogger<AutoTaggingService>? logger = null)
    {
        _logger = logger;
        _keywordMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _categoryMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        LoadKeywordsFromJson();
    }

    private void LoadKeywordsFromJson()
    {
        try
        {
            // Try to find keywords.json in Data folder relative to executable
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var jsonPath = Path.Combine(basePath, "Data", "keywords.json");

            // Fallback to development path
            if (!File.Exists(jsonPath))
            {
                jsonPath = Path.Combine(basePath, "..", "..", "..", "Data", "keywords.json");
            }

            if (File.Exists(jsonPath))
            {
                var jsonContent = File.ReadAllText(jsonPath);
                var keywordData = JsonSerializer.Deserialize<KeywordDatabase>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (keywordData?.Categories != null)
                {
                    foreach (var category in keywordData.Categories)
                    {
                        var categoryName = category.Value.DefaultCategory ?? category.Key;
                        
                        foreach (var keyword in category.Value.Keywords)
                        {
                            if (!_keywordMappings.ContainsKey(keyword))
                            {
                                _keywordMappings[keyword] = categoryName;
                                _categoryMappings[keyword] = categoryName;
                            }
                        }
                    }
                }

                if (keywordData?.StopWords != null)
                {
                    foreach (var word in keywordData.StopWords)
                    {
                        _stopWords.Add(word);
                    }
                }

                _logger?.LogInformation("Loaded {Count} keywords from keywords.json", _keywordMappings.Count);
            }
            else
            {
                _logger?.LogWarning("keywords.json not found, using default keywords");
                LoadDefaultKeywords();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load keywords.json, using defaults");
            LoadDefaultKeywords();
        }
    }

    private void LoadDefaultKeywords()
    {
        // Fallback default keywords
        var defaults = new Dictionary<string, string>
        {
            { "starwars", "Star Wars" }, { "vader", "Star Wars" }, { "yoda", "Star Wars" },
            { "marvel", "Marvel" }, { "ironman", "Marvel" }, { "spiderman", "Marvel" },
            { "batman", "DC Comics" }, { "joker", "DC Comics" },
            { "pokemon", "Pokemon" }, { "pikachu", "Pokemon" },
            { "mario", "Gaming" }, { "zelda", "Gaming" }, { "minecraft", "Gaming" },
            { "gridfinity", "Functional" }, { "holder", "Functional" }, { "mount", "Functional" },
            { "benchy", "Calibration" }, { "calibration", "Calibration" },
            { "vase", "Home" }, { "lamp", "Home" }, { "planter", "Home" },
            { "dnd", "Tabletop" }, { "miniature", "Tabletop" }, { "terrain", "Tabletop" }
        };

        foreach (var kvp in defaults)
        {
            _keywordMappings[kvp.Key] = kvp.Value;
            _categoryMappings[kvp.Key] = kvp.Value;
        }
    }

    public HashSet<string> GenerateTags(string filePath)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        if (string.IsNullOrWhiteSpace(filePath))
            return tags;
        
        var filename = Path.GetFileNameWithoutExtension(filePath);
        var directoryName = Path.GetDirectoryName(filePath);
        var parentDir = directoryName != null ? Path.GetFileName(directoryName) : null;

        // Process filename tokens
        ExtractTagsFromText(filename, tags);

        // Process parent directory name
        if (!string.IsNullOrEmpty(parentDir) && parentDir.Length > 2)
        {
            ExtractTagsFromText(parentDir, tags);
        }

        return tags;
    }

    /// <summary>
    /// Suggests a category based on filename keywords.
    /// </summary>
    public string? SuggestCategory(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return null;

        var normalizedName = filename.ToLowerInvariant();
        var tokens = Regex.Split(normalizedName, @"[\s_\-\.]+")
            .Where(t => t.Length > 2)
            .ToList();

        _logger?.LogInformation("Suggesting category for '{Filename}'. Tokens: {Tokens}", filename, string.Join(", ", tokens));

        // Check for category matches
        foreach (var token in tokens)
        {
            if (_categoryMappings.TryGetValue(token, out var category))
            {
                _logger?.LogInformation("Match found: Token '{Token}' -> Category '{Category}'", token, category);
                return category;
            }
        }

        _logger?.LogInformation("No category match found for '{Filename}'", filename);
        return null;
    }

    private void ExtractTagsFromText(string text, HashSet<string> tags)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var tokens = Regex.Split(text, @"[\s_\-\.]+")
            .Where(t => t.Length > 2)
            .Where(t => !_stopWords.Contains(t))
            .Distinct();

        foreach (var token in tokens)
        {
            // Ignore version numbers
            if (Regex.IsMatch(token, @"^v?\d+(\.\d+)?$")) continue;

            // Check mapping
            if (_keywordMappings.TryGetValue(token, out var mappedTag))
            {
                tags.Add(mappedTag);
            }
            
            // Add the token itself as a tag
            string cleanToken = CapitalizeFirstLetter(token);
            tags.Add(cleanToken);
        }
    }

    private static string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }

    // JSON deserialization classes
    private class KeywordDatabase
    {
        public string? Version { get; set; }
        public Dictionary<string, CategoryKeywords>? Categories { get; set; }
        public List<string>? StopWords { get; set; }
    }

    private class CategoryKeywords
    {
        public List<string> Keywords { get; set; } = new();
        public string? DefaultCategory { get; set; }
    }
}
