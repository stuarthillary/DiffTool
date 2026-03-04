namespace DiffTool.Core.Models;

/// <summary>
/// Represents a point-in-time snapshot of a directory tree.
/// </summary>
public sealed class DirectorySnapshot
{
    /// <summary>
    /// The absolute path of the root directory that was scanned.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// UTC timestamp when the snapshot was captured.
    /// </summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>
    /// Sorted list of file entries in the snapshot (sorted by relative path for deterministic output).
    /// </summary>
    public required IReadOnlyList<FileEntry> Entries { get; init; }

    /// <summary>
    /// Schema version for forward-compatible deserialization.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Name of the machine that captured the snapshot.
    /// </summary>
    public string? MachineName { get; init; }
}
