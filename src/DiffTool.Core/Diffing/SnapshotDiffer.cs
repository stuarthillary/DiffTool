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
        var entries = new List<DiffEntry>();
        var leftEntries = left.Entries;
        var rightEntries = right.Entries;
        
        int leftIndex = 0;
        int rightIndex = 0;
        
        while (leftIndex < leftEntries.Count || rightIndex < rightEntries.Count)
        {
            FileEntry? leftEntry = leftIndex < leftEntries.Count ? leftEntries[leftIndex] : null;
            FileEntry? rightEntry = rightIndex < rightEntries.Count ? rightEntries[rightIndex] : null;
            
            int comparison;
            if (leftEntry == null)
            {
                comparison = 1;
            }
            else if (rightEntry == null)
            {
                comparison = -1;
            }
            else
            {
                comparison = string.Compare(leftEntry.RelativePath, rightEntry.RelativePath, StringComparison.Ordinal);
            }
            
            if (comparison < 0)
            {
                entries.Add(new DiffEntry
                {
                    RelativePath = leftEntry!.RelativePath,
                    Kind = DiffKind.Removed,
                    Left = leftEntry,
                    Right = null,
                    ChangedProperties = []
                });
                leftIndex++;
            }
            else if (comparison > 0)
            {
                entries.Add(new DiffEntry
                {
                    RelativePath = rightEntry!.RelativePath,
                    Kind = DiffKind.Added,
                    Left = null,
                    Right = rightEntry,
                    ChangedProperties = []
                });
                rightIndex++;
            }
            else
            {
                var leftFile = leftEntry!;
                var rightFile = rightEntry!;
                var changedProps = new List<string>();
                bool isUnchanged = false;
                
                if (leftFile.Hash != null && rightFile.Hash != null && leftFile.Hash == rightFile.Hash)
                {
                    isUnchanged = true;
                }
                else
                {
                    if (leftFile.Hash != rightFile.Hash && leftFile.Hash != null && rightFile.Hash != null)
                    {
                        changedProps.Add("Hash");
                    }
                    
                    if (leftFile.SizeBytes != rightFile.SizeBytes)
                    {
                        changedProps.Add("SizeBytes");
                    }
                    
                    if (leftFile.LastModifiedUtc != rightFile.LastModifiedUtc)
                    {
                        changedProps.Add("LastModifiedUtc");
                    }
                    
                    if (leftFile.Attributes != rightFile.Attributes && leftFile.Attributes != null && rightFile.Attributes != null)
                    {
                        changedProps.Add("Attributes");
                    }
                    
                    if (leftFile.IsDirectory != rightFile.IsDirectory)
                    {
                        changedProps.Add("IsDirectory");
                    }
                }
                
                entries.Add(new DiffEntry
                {
                    RelativePath = leftFile.RelativePath,
                    Kind = isUnchanged ? DiffKind.Unchanged : (changedProps.Count > 0 ? DiffKind.Modified : DiffKind.Unchanged),
                    Left = leftFile,
                    Right = rightFile,
                    ChangedProperties = changedProps.AsReadOnly()
                });
                
                leftIndex++;
                rightIndex++;
            }
        }
        
        int added = entries.Count(e => e.Kind == DiffKind.Added);
        int removed = entries.Count(e => e.Kind == DiffKind.Removed);
        int modified = entries.Count(e => e.Kind == DiffKind.Modified);
        int unchanged = entries.Count(e => e.Kind == DiffKind.Unchanged);
        
        return new SnapshotDiff
        {
            LeftPath = left.RootPath,
            RightPath = right.RootPath,
            LeftCapturedAtUtc = left.CapturedAtUtc,
            RightCapturedAtUtc = right.CapturedAtUtc,
            Entries = entries.AsReadOnly(),
            Summary = new DiffSummary
            {
                Added = added,
                Removed = removed,
                Modified = modified,
                Unchanged = unchanged
            }
        };
    }
}
