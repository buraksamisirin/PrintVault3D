using System.IO;
using Microsoft.EntityFrameworkCore;
using PrintVault3D.Data;
using PrintVault3D.Models;

namespace PrintVault3D.Repositories;

/// <summary>
/// Repository implementation for Gcode-specific operations.
/// </summary>
public class GcodeRepository : Repository<Gcode>, IGcodeRepository
{
    public GcodeRepository(PrintVaultDbContext context) : base(context)
    {
    }

    public async Task<Gcode?> GetByFilePathAsync(string filePath)
    {
        return await _dbSet
            .Include(g => g.Model)
            .FirstOrDefaultAsync(g => g.FilePath == filePath);
    }

    public async Task<Gcode?> GetByFileHashAsync(string fileHash)
    {
        return await _dbSet
            .Include(g => g.Model)
            .FirstOrDefaultAsync(g => g.FileHash == fileHash);
    }

    public async Task<IEnumerable<Gcode>> GetByModelIdAsync(int modelId)
    {
        return await _dbSet
            .Where(g => g.ModelId == modelId)
            .OrderByDescending(g => g.AddedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Gcode>> GetUnlinkedAsync()
    {
        return await _dbSet
            .Where(g => g.ModelId == null)
            .OrderByDescending(g => g.AddedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<(Model3D Model, int Score)>> FindPotentialMatchesAsync(string gcodeFilename)
    {
        // Normalize the G-code filename
        var normalizedGcode = NormalizeFilename(gcodeFilename);
        
        if (string.IsNullOrWhiteSpace(normalizedGcode))
        {
            return Enumerable.Empty<(Model3D, int)>();
        }

        // Extract keywords from the gcode filename for database filtering
        var keywords = normalizedGcode
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(k => k.Length >= 3) // Only meaningful words
            .Take(5) // Limit keywords
            .ToList();

        // First, filter models in the database using keywords
        // This avoids loading ALL models into memory
        IQueryable<Model3D> query = _context.Models;
        
        if (keywords.Count > 0)
        {
            // Build a query that finds models containing any of the keywords using LIKE for case-insensitivity
            var keywordPatterns = keywords.Select(k => $"%{k}%").ToList();
            query = query.Where(m => keywordPatterns.Any(p => EF.Functions.Like(m.Name, p)));
        }
        else
        {
            // Fallback: if no keywords, use the whole normalized name with LIKE
            var pattern = $"%{normalizedGcode}%";
            query = query.Where(m => EF.Functions.Like(m.Name, pattern));
        }

        // Limit results to prevent memory issues
        var candidateModels = await query.Take(100).ToListAsync();

        // Now do the detailed scoring in memory with the filtered subset
        var matches = new List<(Model3D Model, int Score)>();

        foreach (var model in candidateModels)
        {
            var normalizedModel = NormalizeFilename(model.Name);
            var score = CalculateSimilarityScore(normalizedGcode, normalizedModel);
            
            if (score > 30) // Minimum threshold
            {
                matches.Add((model, score));
            }
        }

        return matches.OrderByDescending(m => m.Score);
    }

    public async Task<IEnumerable<Gcode>> GetAllWithModelsAsync()
    {
        return await _dbSet
            .Include(g => g.Model)
            .OrderByDescending(g => g.AddedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Normalizes a filename by removing extensions, common suffixes, and special characters.
    /// </summary>
    private static string NormalizeFilename(string filename)
    {
        // Remove extension
        var name = Path.GetFileNameWithoutExtension(filename);
        
        // Common suffixes to remove from G-code filenames
        var suffixesToRemove = new[] { 
            "_0.2mm", "_0.3mm", "_0.1mm",  // Layer heights
            "_PLA", "_PETG", "_ABS",        // Materials
            "_slow", "_fast", "_draft",     // Speed profiles
            "_supports", "_nosupports"      // Support settings
        };

        foreach (var suffix in suffixesToRemove)
        {
            name = name.Replace(suffix, "", StringComparison.OrdinalIgnoreCase);
        }

        // Convert to lowercase and remove special characters
        return new string(name.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray())
            .Trim();
    }

    /// <summary>
    /// Calculates similarity score between two normalized filenames.
    /// Returns a score from 0-100.
    /// </summary>
    private static int CalculateSimilarityScore(string gcode, string model)
    {
        if (string.IsNullOrWhiteSpace(gcode) || string.IsNullOrWhiteSpace(model))
            return 0;

        // Exact match
        if (gcode == model)
            return 100;

        // Check if one contains the other
        if (gcode.Contains(model) || model.Contains(gcode))
        {
            var containmentRatio = (double)Math.Min(gcode.Length, model.Length) / 
                                   Math.Max(gcode.Length, model.Length);
            return (int)(containmentRatio * 90);
        }

        // Word-based matching
        var gcodeWords = gcode.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var modelWords = model.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (gcodeWords.Count == 0 || modelWords.Count == 0)
            return 0;

        var intersection = gcodeWords.Intersect(modelWords).Count();
        var union = gcodeWords.Union(modelWords).Count();

        // Jaccard similarity
        return (int)((double)intersection / union * 80);
    }
}

