namespace DiffTool.Core.Models;

/// <summary>
/// Metadata for a single file or directory within a snapshot.
/// </summary>
public sealed class FileEntry
{
    /// <summary>
    /// Path relative to the snapshot root, using forward slashes for cross-platform consistency.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// File size in bytes. Null for directories.
    /// </summary>
    public long? SizeBytes { get; init; }

    /// <summary>
    /// SHA-256 hash of file contents, lowercase hex. Null for directories.
    /// </summary>
    public string? Hash { get; init; }

    /// <summary>
    /// Last modification time in UTC.
    /// </summary>
    public required DateTimeOffset LastModifiedUtc { get; init; }

    /// <summary>
    /// Whether this entry represents a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// File attributes/permissions as a string (e.g., "rwxr-xr-x" on Unix, "ReadOnly, Archive" on Windows).
    /// </summary>
    public string? Attributes { get; init; }

    /// <summary>
    /// .NET assembly metadata extracted by <see cref="DiffTool.Core.Scanning.IAssemblyInspector"/>.
    /// Populated only for .dll/.exe files when an inspector is configured on <see cref="DiffTool.Core.Scanning.DirectoryScanner"/>.
    /// Null for directories, non-.NET binaries, and unreadable files.
    /// </summary>
    public AssemblyInfo? AssemblyInfo { get; init; }
}
