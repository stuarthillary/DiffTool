namespace DiffTool.Core.Models;

/// <summary>
/// Represents a single file's diff result between two snapshots.
/// </summary>
public sealed class DiffEntry
{
    /// <summary>
    /// Relative path of the file (from either snapshot).
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The kind of change detected.
    /// </summary>
    public required DiffKind Kind { get; init; }

    /// <summary>
    /// The file entry from the left (older) snapshot. Null if <see cref="Kind"/> is <see cref="DiffKind.Added"/>.
    /// </summary>
    public FileEntry? Left { get; init; }

    /// <summary>
    /// The file entry from the right (newer) snapshot. Null if <see cref="Kind"/> is <see cref="DiffKind.Removed"/>.
    /// </summary>
    public FileEntry? Right { get; init; }

    /// <summary>
    /// List of property names that changed (e.g., "Hash", "SizeBytes", "Attributes"). Empty for Added/Removed.
    /// </summary>
    public IReadOnlyList<string> ChangedProperties { get; init; } = [];

    /// <summary>
    /// Structural diff of the assembly's public API surface. Populated only when <see cref="Kind"/>
    /// is <see cref="DiffKind.Modified"/>, both <see cref="Left"/> and <see cref="Right"/> carry
    /// <see cref="FileEntry.AssemblyInfo"/>, and the API surface actually changed.
    /// Null for non-assembly files, Added/Removed entries, and when no inspector was configured.
    /// </summary>
    public AssemblyDiff? AssemblyDiff { get; init; }
}
