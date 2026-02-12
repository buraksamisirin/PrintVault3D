using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrintVault3D.Data;
using PrintVault3D.Models;

namespace PrintVault3D.Services;

/// <summary>
/// Service for learning from user corrections to improve auto-tagging.
/// </summary>
public class TagLearningService : ITagLearningService
{
    private readonly PrintVaultDbContext _context;
    private readonly ILogger<TagLearningService>? _logger;

    public TagLearningService(PrintVaultDbContext context, ILogger<TagLearningService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RecordCorrectionAsync(string filename, string? categoryName, string? tags)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return;

        var patterns = ExtractPatterns(filename);
        
        foreach (var pattern in patterns)
        {
            var existing = await _context.TagLearnings
                .FirstOrDefaultAsync(t => t.Pattern == pattern);

            if (existing != null)
            {
                // Update existing pattern
                if (!string.IsNullOrEmpty(categoryName))
                    existing.LearnedCategory = categoryName;
                if (!string.IsNullOrEmpty(tags))
                    existing.LearnedTags = MergeTags(existing.LearnedTags, tags);
                
                existing.UseCount++;
                existing.LastUsed = DateTime.UtcNow;
                
                _logger?.LogDebug("Updated learning for pattern: {Pattern}", pattern);
            }
            else
            {
                // Create new learning
                var learning = new TagLearning
                {
                    Pattern = pattern,
                    LearnedCategory = categoryName,
                    LearnedTags = tags,
                    UseCount = 1,
                    CreatedDate = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow
                };
                
                _context.TagLearnings.Add(learning);
                _logger?.LogDebug("Created new learning for pattern: {Pattern}", pattern);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<LearnedSuggestion?> GetLearnedSuggestionAsync(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return null;

        var patterns = ExtractPatterns(filename);
        
        // Find the best matching learned pattern (highest use count)
        TagLearning? bestMatch = null;
        string matchedPattern = "";

        foreach (var pattern in patterns)
        {
            var learning = await _context.TagLearnings
                .Where(t => t.Pattern == pattern)
                .OrderByDescending(t => t.UseCount)
                .FirstOrDefaultAsync();

            if (learning != null && (bestMatch == null || learning.UseCount > bestMatch.UseCount))
            {
                bestMatch = learning;
                matchedPattern = pattern;
            }
        }

        if (bestMatch == null)
            return null;

        _logger?.LogInformation("Found learned suggestion for {Filename}: Category={Category}, Pattern={Pattern}", 
            filename, bestMatch.LearnedCategory, matchedPattern);

        return new LearnedSuggestion
        {
            CategoryName = bestMatch.LearnedCategory,
            Tags = bestMatch.LearnedTags,
            Confidence = Math.Min(100, bestMatch.UseCount * 20), // Max 100%
            MatchedPattern = matchedPattern
        };
    }

    public async Task ConfirmPatternAsync(string pattern)
    {
        var learning = await _context.TagLearnings
            .FirstOrDefaultAsync(t => t.Pattern == pattern);

        if (learning != null)
        {
            learning.UseCount++;
            learning.LastUsed = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Extracts meaningful patterns from a filename for learning.
    /// </summary>
    private static List<string> ExtractPatterns(string filename)
    {
        var patterns = new List<string>();
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filename).ToLowerInvariant();
        
        // Split by common delimiters
        var regex = new Regex(@"[\s_\-\.]+");
        var tokens = regex.Split(nameWithoutExt)
            .Where(t => t.Length > 2)
            .Where(t => !Regex.IsMatch(t, @"^v?\d+(\.\d+)?$")) // Skip version numbers
            .ToList();

        // Add individual tokens as patterns
        patterns.AddRange(tokens);

        // Add consecutive token pairs (compound patterns)
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            patterns.Add($"{tokens[i]}_{tokens[i + 1]}");
        }

        return patterns.Distinct().ToList();
    }

    /// <summary>
    /// Merges existing tags with new tags, avoiding duplicates.
    /// </summary>
    private static string MergeTags(string? existing, string? newTags)
    {
        var existingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        if (!string.IsNullOrEmpty(existing))
        {
            foreach (var tag in existing.Split(',', StringSplitOptions.RemoveEmptyEntries))
                existingSet.Add(tag.Trim());
        }

        if (!string.IsNullOrEmpty(newTags))
        {
            foreach (var tag in newTags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                existingSet.Add(tag.Trim());
        }

        return string.Join(", ", existingSet);
    }
}
