namespace PrintVault3D.Models;

/// <summary>
/// Status of a GCODE print job.
/// </summary>
public enum PrintStatus
{
    /// <summary>Not yet printed.</summary>
    NotPrinted = 0,
    
    /// <summary>Currently printing.</summary>
    InProgress = 1,
    
    /// <summary>Print completed successfully.</summary>
    Success = 2,
    
    /// <summary>Print failed.</summary>
    Failed = 3,
    
    /// <summary>Print was cancelled.</summary>
    Cancelled = 4
}
