namespace DiffTool.Core.Models;

/// <summary>
/// The kind of change detected for a file entry between two snapshots.
/// </summary>
public enum DiffKind
{
    /// <summary>File exists in both snapshots and is identical.</summary>
    Unchanged,

    /// <summary>File exists only in the right (newer) snapshot.</summary>
    Added,

    /// <summary>File exists only in the left (older) snapshot.</summary>
    Removed,

    /// <summary>File exists in both snapshots but metadata differs.</summary>
    Modified
}
