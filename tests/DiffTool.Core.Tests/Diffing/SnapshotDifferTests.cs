using DiffTool.Core.Diffing;
using DiffTool.Core.Models;

namespace DiffTool.Core.Tests.Diffing;

public sealed class SnapshotDifferTests
{
    private static readonly DateTimeOffset FixedTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FixedTime2 = new(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static DirectorySnapshot MakeSnapshot(string rootPath, DateTimeOffset capturedAt, params FileEntry[] entries)
    {
        return new DirectorySnapshot
        {
            RootPath = rootPath,
            CapturedAtUtc = capturedAt,
            Entries = entries.ToList()
        };
    }

    [Test]
    public async Task BothSnapshotsEmpty_ReturnsEmptyDiff()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime);
        var right = MakeSnapshot("C:/right", FixedTime2);

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(0);
        await Assert.That(result.Summary.Added).IsEqualTo(0);
        await Assert.That(result.Summary.Removed).IsEqualTo(0);
        await Assert.That(result.Summary.Modified).IsEqualTo(0);
        await Assert.That(result.Summary.Unchanged).IsEqualTo(0);
        await Assert.That(result.Summary.Total).IsEqualTo(0);
    }

    [Test]
    public async Task LeftEmpty_RightHasFiles_AllAdded()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime);
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime },
            new FileEntry { RelativePath = "file2.txt", SizeBytes = 200, Hash = "def456", LastModifiedUtc = FixedTime });

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(2);
        await Assert.That(result.Summary.Added).IsEqualTo(2);
        await Assert.That(result.Summary.Removed).IsEqualTo(0);
        await Assert.That(result.Summary.Modified).IsEqualTo(0);
        await Assert.That(result.Summary.Unchanged).IsEqualTo(0);
        await Assert.That(result.Summary.Total).IsEqualTo(2);
        
        await Assert.That(result.Entries[0].Kind).IsEqualTo(DiffKind.Added);
        await Assert.That(result.Entries[0].RelativePath).IsEqualTo("file1.txt");
        await Assert.That(result.Entries[1].Kind).IsEqualTo(DiffKind.Added);
        await Assert.That(result.Entries[1].RelativePath).IsEqualTo("file2.txt");
    }

    [Test]
    public async Task LeftHasFiles_RightEmpty_AllRemoved()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime },
            new FileEntry { RelativePath = "file2.txt", SizeBytes = 200, Hash = "def456", LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2);

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(2);
        await Assert.That(result.Summary.Added).IsEqualTo(0);
        await Assert.That(result.Summary.Removed).IsEqualTo(2);
        await Assert.That(result.Summary.Modified).IsEqualTo(0);
        await Assert.That(result.Summary.Unchanged).IsEqualTo(0);
        await Assert.That(result.Summary.Total).IsEqualTo(2);
        
        await Assert.That(result.Entries[0].Kind).IsEqualTo(DiffKind.Removed);
        await Assert.That(result.Entries[0].RelativePath).IsEqualTo("file1.txt");
        await Assert.That(result.Entries[1].Kind).IsEqualTo(DiffKind.Removed);
        await Assert.That(result.Entries[1].RelativePath).IsEqualTo("file2.txt");
    }

    [Test]
    public async Task IdenticalSnapshots_AllUnchanged()
    {
        var differ = new SnapshotDiffer();
        var entry1 = new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime };
        var entry2 = new FileEntry { RelativePath = "file2.txt", SizeBytes = 200, Hash = "def456", LastModifiedUtc = FixedTime };
        var left = MakeSnapshot("C:/left", FixedTime, entry1, entry2);
        var right = MakeSnapshot("C:/right", FixedTime2, entry1, entry2);

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(2);
        await Assert.That(result.Summary.Added).IsEqualTo(0);
        await Assert.That(result.Summary.Removed).IsEqualTo(0);
        await Assert.That(result.Summary.Modified).IsEqualTo(0);
        await Assert.That(result.Summary.Unchanged).IsEqualTo(2);
        await Assert.That(result.Summary.Total).IsEqualTo(2);
        
        await Assert.That(result.Entries[0].Kind).IsEqualTo(DiffKind.Unchanged);
        await Assert.That(result.Entries[1].Kind).IsEqualTo(DiffKind.Unchanged);
    }

    [Test]
    public async Task FileAddedInRight_SingleAddedEntry()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime },
            new FileEntry { RelativePath = "file2.txt", SizeBytes = 200, Hash = "def456", LastModifiedUtc = FixedTime });

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(2);
        await Assert.That(result.Summary.Added).IsEqualTo(1);
        await Assert.That(result.Summary.Removed).IsEqualTo(0);
        await Assert.That(result.Summary.Modified).IsEqualTo(0);
        await Assert.That(result.Summary.Unchanged).IsEqualTo(1);
        await Assert.That(result.Summary.Total).IsEqualTo(2);
        
        await Assert.That(result.Entries[1].Kind).IsEqualTo(DiffKind.Added);
        await Assert.That(result.Entries[1].RelativePath).IsEqualTo("file2.txt");
        await Assert.That(result.Entries[1].Left).IsNull();
        await Assert.That(result.Entries[1].Right).IsNotNull();
    }

    [Test]
    public async Task FileRemovedFromRight_SingleRemovedEntry()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime },
            new FileEntry { RelativePath = "file2.txt", SizeBytes = 200, Hash = "def456", LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime });

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(2);
        await Assert.That(result.Summary.Added).IsEqualTo(0);
        await Assert.That(result.Summary.Removed).IsEqualTo(1);
        await Assert.That(result.Summary.Modified).IsEqualTo(0);
        await Assert.That(result.Summary.Unchanged).IsEqualTo(1);
        await Assert.That(result.Summary.Total).IsEqualTo(2);
        
        await Assert.That(result.Entries[1].Kind).IsEqualTo(DiffKind.Removed);
        await Assert.That(result.Entries[1].RelativePath).IsEqualTo("file2.txt");
        await Assert.That(result.Entries[1].Left).IsNotNull();
        await Assert.That(result.Entries[1].Right).IsNull();
    }

    [Test]
    public async Task HashChanged_ModifiedWithHashInChangedProperties()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "xyz789", LastModifiedUtc = FixedTime });

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(1);
        await Assert.That(result.Summary.Modified).IsEqualTo(1);
        await Assert.That(result.Summary.Total).IsEqualTo(1);
        
        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Modified);
        await Assert.That(entry.ChangedProperties.Contains("Hash")).IsTrue();
    }

    [Test]
    public async Task SizeChangedOnly_ModifiedWithSizeBytesInChangedProperties()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = null, LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 200, Hash = null, LastModifiedUtc = FixedTime });

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(1);
        await Assert.That(result.Summary.Modified).IsEqualTo(1);
        
        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Modified);
        await Assert.That(entry.ChangedProperties.Contains("SizeBytes")).IsTrue();
    }

    [Test]
    public async Task HashEqual_UnchangedEvenIfLastModifiedDiffers()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file1.txt", SizeBytes = 100, Hash = "abc123", LastModifiedUtc = FixedTime2 });

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(1);
        await Assert.That(result.Summary.Unchanged).IsEqualTo(1);
        await Assert.That(result.Summary.Modified).IsEqualTo(0);
        
        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Unchanged);
    }

    [Test]
    public async Task MultipleChanges_MixedDiffKinds_CorrectSummaryCounts()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "added.txt", SizeBytes = 100, Hash = "a1", LastModifiedUtc = FixedTime }, // Will be removed
            new FileEntry { RelativePath = "modified.txt", SizeBytes = 100, Hash = "m1", LastModifiedUtc = FixedTime },
            new FileEntry { RelativePath = "unchanged.txt", SizeBytes = 100, Hash = "u1", LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "modified.txt", SizeBytes = 200, Hash = "m2", LastModifiedUtc = FixedTime },
            new FileEntry { RelativePath = "new.txt", SizeBytes = 300, Hash = "n1", LastModifiedUtc = FixedTime }, // Added
            new FileEntry { RelativePath = "unchanged.txt", SizeBytes = 100, Hash = "u1", LastModifiedUtc = FixedTime });

        var result = differ.Compare(left, right);

        await Assert.That(result.Summary.Added).IsEqualTo(1);
        await Assert.That(result.Summary.Removed).IsEqualTo(1);
        await Assert.That(result.Summary.Modified).IsEqualTo(1);
        await Assert.That(result.Summary.Unchanged).IsEqualTo(1);
        await Assert.That(result.Summary.Total).IsEqualTo(4);
        await Assert.That(result.Entries.Count).IsEqualTo(4);
    }

    [Test]
    public async Task AddedEntry_ChangedPropertiesIsEmpty()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime);
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "new.txt", SizeBytes = 100, Hash = "abc", LastModifiedUtc = FixedTime });

        var result = differ.Compare(left, right);

        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Added);
        await Assert.That(entry.ChangedProperties.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RemovedEntry_ChangedPropertiesIsEmpty()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "old.txt", SizeBytes = 100, Hash = "abc", LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2);

        var result = differ.Compare(left, right);

        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Removed);
        await Assert.That(entry.ChangedProperties.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SnapshotDiff_CarriesCorrectMetadata()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime);
        var right = MakeSnapshot("C:/right", FixedTime2);

        var result = differ.Compare(left, right);

        await Assert.That(result.LeftPath).IsEqualTo("C:/left");
        await Assert.That(result.RightPath).IsEqualTo("C:/right");
        await Assert.That(result.LeftCapturedAtUtc).IsEqualTo(FixedTime);
        await Assert.That(result.RightCapturedAtUtc).IsEqualTo(FixedTime2);
    }

    [Test]
    public async Task LastModifiedUtcChanged_NoHashes_Modified()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file.txt", SizeBytes = 100, Hash = null, LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file.txt", SizeBytes = 100, Hash = null, LastModifiedUtc = FixedTime2 });

        var result = differ.Compare(left, right);

        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Modified);
        await Assert.That(entry.ChangedProperties.Contains("LastModifiedUtc")).IsTrue();
    }

    [Test]
    public async Task AttributesChanged_Modified()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file.txt", SizeBytes = 100, Hash = null, LastModifiedUtc = FixedTime, Attributes = "ReadOnly" });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file.txt", SizeBytes = 100, Hash = null, LastModifiedUtc = FixedTime, Attributes = "Archive" });

        var result = differ.Compare(left, right);

        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Modified);
        await Assert.That(entry.ChangedProperties.Contains("Attributes")).IsTrue();
    }

    [Test]
    public async Task IsDirectoryChanged_Modified()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "path", SizeBytes = null, Hash = null, LastModifiedUtc = FixedTime, IsDirectory = true });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "path", SizeBytes = 100, Hash = "abc", LastModifiedUtc = FixedTime, IsDirectory = false });

        var result = differ.Compare(left, right);

        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Modified);
        await Assert.That(entry.ChangedProperties.Contains("IsDirectory")).IsTrue();
    }

    [Test]
    public async Task MultiplePropertiesChanged_AllListedInChangedProperties()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "file.txt", SizeBytes = 100, Hash = "abc", LastModifiedUtc = FixedTime, Attributes = "ReadOnly" });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "file.txt", SizeBytes = 200, Hash = "xyz", LastModifiedUtc = FixedTime2, Attributes = "Archive" });

        var result = differ.Compare(left, right);

        var entry = result.Entries[0];
        await Assert.That(entry.Kind).IsEqualTo(DiffKind.Modified);
        await Assert.That(entry.ChangedProperties.Contains("Hash")).IsTrue();
        await Assert.That(entry.ChangedProperties.Contains("SizeBytes")).IsTrue();
        await Assert.That(entry.ChangedProperties.Contains("LastModifiedUtc")).IsTrue();
        await Assert.That(entry.ChangedProperties.Contains("Attributes")).IsTrue();
        await Assert.That(entry.ChangedProperties.Count).IsEqualTo(4);
    }

    [Test]
    public async Task EntriesAreSortedByRelativePath()
    {
        var differ = new SnapshotDiffer();
        var left = MakeSnapshot("C:/left", FixedTime,
            new FileEntry { RelativePath = "a.txt", SizeBytes = 100, Hash = "a", LastModifiedUtc = FixedTime },
            new FileEntry { RelativePath = "c.txt", SizeBytes = 100, Hash = "c", LastModifiedUtc = FixedTime });
        var right = MakeSnapshot("C:/right", FixedTime2,
            new FileEntry { RelativePath = "b.txt", SizeBytes = 100, Hash = "b", LastModifiedUtc = FixedTime },
            new FileEntry { RelativePath = "c.txt", SizeBytes = 100, Hash = "c", LastModifiedUtc = FixedTime });

        var result = differ.Compare(left, right);

        await Assert.That(result.Entries.Count).IsEqualTo(3);
        await Assert.That(result.Entries[0].RelativePath).IsEqualTo("a.txt");
        await Assert.That(result.Entries[1].RelativePath).IsEqualTo("b.txt");
        await Assert.That(result.Entries[2].RelativePath).IsEqualTo("c.txt");
    }
}
