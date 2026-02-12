using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PrintVault3D.Services;

public class GcodeParserService : IGcodeParserService
{
    private readonly ILogger<GcodeParserService>? _logger;

    public GcodeParserService(ILogger<GcodeParserService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<GcodeMetadata> ParseAsync(string filePath)
    {
        var metadata = new GcodeMetadata();
        
        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("G-code file not found: {FilePath}", filePath);
            return metadata;
        }

        try
        {
            const int HEAD_LINES_TO_READ = 500;
            const int TAIL_BYTES_TO_READ = 512000; // 500 KB - Aggressive scan for large files

            var content = new System.Text.StringBuilder();

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                // 1. Read Head
                int linesRead = 0;
                string? line;
                while (linesRead < HEAD_LINES_TO_READ && (line = await reader.ReadLineAsync()) != null)
                {
                    content.AppendLine(line);
                    linesRead++;
                }

                // 2. Read Tail
                if (stream.Length > TAIL_BYTES_TO_READ)
                {
                    stream.Seek(-TAIL_BYTES_TO_READ, SeekOrigin.End);
                    // Discard first partial line after seek
                    await reader.ReadLineAsync(); 
                    var tail = await reader.ReadToEndAsync();
                    content.AppendLine(tail);
                }
                else if (stream.Length > 0 && linesRead >= HEAD_LINES_TO_READ)
                {
                     var remaining = await reader.ReadToEndAsync();
                     content.AppendLine(remaining);
                }
            }

            var fullText = content.ToString();
            
            ParseContent(fullText, metadata);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing G-code file: {FilePath}", filePath);
        }

        return metadata;
    }

    // Standard/Cura
    private static readonly Regex SlicerRegex = new(@";\s*(?:Generated with|Slicer\s*=|GENERATOR:)\s*(?<name>[\w\s]+)(?:\s(?<version>[\d\.]+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CuraTimeRegex = new(@";TIME:(\d+)", RegexOptions.Compiled);
    
    // PrusaSlicer / SuperSlicer
    private static readonly Regex PrusaTimeRegex = new(@";\s*estimated printing time\s*=\s*(.*)", RegexOptions.Compiled);
    
    // Bambu Studio / OrcaSlicer
    private static readonly Regex BambuTimeRegex = new(@";\s*model printing time\s*=\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BambuFilamentGramRegex = new(@";\s*total filament used \[g\]\s*=\s*([\d\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BambuFilamentMmRegex = new(@";\s*filament used \[mm\]\s*=\s*([\d\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BambuLayerHeightRegex = new(@";\s*layer_height\s*=\s*([\d\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Common
    private static readonly Regex FilamentMmRegex = new(@";\s*(?:Filament used:\s*([\d\.]+)m|filament used \[mm\]\s*=\s*([\d\.]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FilamentGramRegex = new(@";\s*filament used \[g\]\s*=\s*([\d\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LayerHeightRegex = new(@";\s*(?:Layer height:|layer_height\s*=)\s*([\d\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Added sparse_infill_density for Bambu/Orca
    private static readonly Regex InfillRegex = new(@";\s*(?:Infill Density:|fill_density\s*=|sparse_infill_density\s*=)\s*([\d\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Temperatures
    private static readonly Regex NozzleTempRegex = new(@";\s*(?:nozzle_temperature|first_layer_temperature|nozzle_temperature_initial_layer)\s*=\s*(\d+)", RegexOptions.Compiled);
    // Updated to support decimals (e.g. S210.0)
    private static readonly Regex M104Regex = new(@"M10[49]\s+S([\d\.]+)", RegexOptions.Compiled);
    private static readonly Regex BedTempRegex = new(@";\s*(?:bed_temperature|first_layer_bed_temperature|bed_temperature_initial_layer_single)\s*=\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex M140Regex = new(@"M1(?:40|90)\s+S([\d\.]+)", RegexOptions.Compiled);
    
    private static readonly Regex NozzleDiaRegex = new(@";\s*nozzle_diameter\s*=\s*([\d\.]+)", RegexOptions.Compiled);

    private void ParseContent(string text, GcodeMetadata metadata)
    {
        // --- 1. Slicer Name & Version ---
        var slicerMatch = SlicerRegex.Match(text);
        if (slicerMatch.Success)
        {
            metadata.SlicerName = slicerMatch.Groups["name"].Value.Trim();
            if (slicerMatch.Groups["version"].Success)
                metadata.SlicerVersion = slicerMatch.Groups["version"].Value;
        }

        // --- 2. Print Time ---
        // Cura (Seconds)
        var curaTime = CuraTimeRegex.Match(text);
        if (curaTime.Success && int.TryParse(curaTime.Groups[1].Value, out int seconds))
        {
            metadata.PrintTime = TimeSpan.FromSeconds(seconds);
        }
        else
        {
            // Prusa (Format: 1h 20m 30s)
            var prusaTime = PrusaTimeRegex.Match(text);
            if (prusaTime.Success)
            {
                metadata.PrintTime = ParseTimeBoxFormat(prusaTime.Groups[1].Value);
            }
            else 
            {
                // Bambu/Orca (Format: 1h 20m 30s)
                var bambuTime = BambuTimeRegex.Match(text);
                if (bambuTime.Success)
                {
                     metadata.PrintTime = ParseTimeBoxFormat(bambuTime.Groups[1].Value);
                }
            }
        }

        // --- 3. Filament Used ---
        // Bambu/Orca specific
        var bambuFilWeight = BambuFilamentGramRegex.Match(text);
        if (bambuFilWeight.Success && double.TryParse(bambuFilWeight.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double bg))
        {
             metadata.FilamentUsedGrams = bg;
        }
        else
        {
            // Generic
            var filWeight = FilamentGramRegex.Match(text);
            if (filWeight.Success && double.TryParse(filWeight.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double g))
            {
                metadata.FilamentUsedGrams = g;
            }
        }

        var bambuFilLen = BambuFilamentMmRegex.Match(text);
        if (bambuFilLen.Success && double.TryParse(bambuFilLen.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double bmm))
        {
            metadata.FilamentUsedMm = bmm;
        }
        else
        {
            // Generic
            var filLenMm = FilamentMmRegex.Match(text);
            if (filLenMm.Success)
            {
                if (double.TryParse(filLenMm.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double m)) metadata.FilamentUsedMm = m * 1000;
                else if (double.TryParse(filLenMm.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double mm)) metadata.FilamentUsedMm = mm;
            }
        }

        // --- 4. Layer Height ---
        // Bambu first
        var bambuLayerH = BambuLayerHeightRegex.Match(text);
        if (bambuLayerH.Success && double.TryParse(bambuLayerH.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double blh))
        {
            metadata.LayerHeight = blh;
        }
        else
        {
            var layerH = LayerHeightRegex.Match(text);
            if (layerH.Success && double.TryParse(layerH.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double lh))
            {
                metadata.LayerHeight = lh;
            }
        }

        // --- 5. Infill ---
        var infill = InfillRegex.Match(text);
        if (infill.Success && double.TryParse(infill.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double inf))
        {
            // Handle 0.15 vs 15 format
            if (inf > 0 && inf <= 1) inf *= 100;
            metadata.InfillPercentage = (int)inf;
        }

        // --- 6. Temperatures ---
        var nozzleT = NozzleTempRegex.Match(text);
        if (nozzleT.Success && double.TryParse(nozzleT.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double nt))
        {
            metadata.NozzleTemp = (int)nt;
        }
        else
        {
             var m104 = M104Regex.Match(text);
             if (m104.Success && double.TryParse(m104.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double nt2)) 
                 metadata.NozzleTemp = (int)nt2;
        }

        var bedT = BedTempRegex.Match(text);
        if (bedT.Success && double.TryParse(bedT.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double bt))
        {
            metadata.BedTemp = (int)bt;
        }
        else
        {
             var m140 = M140Regex.Match(text);
             if (m140.Success && double.TryParse(m140.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double bt2)) 
                 metadata.BedTemp = (int)bt2;
        }
        
        // --- 7. Nozzle Diameter ---
        var nozzleD = NozzleDiaRegex.Match(text);
        if (nozzleD.Success && double.TryParse(nozzleD.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double nd))
        {
            metadata.NozzleDiameter = nd;
        }
    }

    private TimeSpan? ParseTimeBoxFormat(string timeString)
    {
        try
        {
            // Format: "1h 50m 5s" or "50m 5s" or "5s"
            // Simple manual parsing
            int hours = 0, minutes = 0, seconds = 0;
            
            var parts = timeString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.EndsWith("h")) int.TryParse(part.TrimEnd('h'), out hours);
                if (part.EndsWith("m")) int.TryParse(part.TrimEnd('m'), out minutes);
                if (part.EndsWith("s")) int.TryParse(part.TrimEnd('s'), out seconds);
                if (part.EndsWith("d")) // Days
                {
                     if (int.TryParse(part.TrimEnd('d'), out int days))
                        hours += days * 24;
                }
            }
            
            return new TimeSpan(hours, minutes, seconds);
        }
        catch
        {
            return null;
        }
    }
}
