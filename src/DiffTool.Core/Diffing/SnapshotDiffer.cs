using DiffTool.Core.Models;

namespace DiffTool.Core.Diffing;

/// <summary>
/// Computes the diff between two <see cref="DirectorySnapshot"/> instances.
/// Uses sorted merge join on relative paths for O(n+m) performance.
/// </summary>
public sealed class SnapshotDiffer
{
    /// <summary>
    /// Compares two snapshots and produces a diff result.
    /// </summary>
    /// <param name="left">The older (baseline) snapshot.</param>
    /// <param name="right">The newer (current) snapshot.</param>
    /// <returns>A diff describing all changes between the two snapshots.</returns>
    public SnapshotDiff Compare(DirectorySnapshot left, DirectorySnapshot right)
    {
        throw new NotImplementedException();
    }
}
