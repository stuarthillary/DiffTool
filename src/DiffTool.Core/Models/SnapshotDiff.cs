namespace DiffTool.Core.Models;

/// <summary>
/// The result of comparing two directory snapshots.
/// </summary>
public sealed class SnapshotDiff
{
    /// <summary>
    /// Root path of the left (older) snapshot.
    /// </summary>
    public required string LeftPath { get; init; }

    /// <summary>
    /// Root path of the right (newer) snapshot.
    /// </summary>
    public required string RightPath { get; init; }

    /// <summary>
    /// When the left snapshot was captured.
    /// </summary>
    public required DateTimeOffset LeftCapturedAtUtc { get; init; }

    /// <summary>
    /// When the right snapshot was captured.
    /// </summary>
    public required DateTimeOffset RightCapturedAtUtc { get; init; }

    /// <summary>
    /// Per-file diff entries, sorted by relative path.
    /// </summary>
    public required IReadOnlyList<DiffEntry> Entries { get; init; }

    /// <summary>
    /// Summary counts by diff kind.
    /// </summary>
    public required DiffSummary Summary { get; init; }
}

/// <summary>
/// Aggregate counts for a snapshot diff.
/// </summary>
public sealed class DiffSummary
{
    public int Added { get; init; }
    public int Removed { get; init; }
    public int Modified { get; init; }
    public int Unchanged { get; init; }
    public int Total => Added + Removed + Modified + Unchanged;
}
