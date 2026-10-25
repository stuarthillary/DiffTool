using DiffTool.Core.Models;
using DiffTool.Core.Scanning;
using AssemblyInfo = DiffTool.Core.Models.AssemblyInfo;

namespace DiffTool.Core.Tests.Scanning;

// ---------------------------------------------------------------------------
// FakeFileSystem — hand-rolled IFileSystem implementation.
// Chosen over Imposter because multi-method setups with per-path return values
// are far cleaner in a real class than in chained mock configuration.
// ---------------------------------------------------------------------------

internal sealed class FakeAssemblyInspector : IAssemblyInspector
{
    private readonly AssemblyInfo? _result;

    public FakeAssemblyInspector(AssemblyInfo? result) => _result = result;

    public int CallCount { get; private set; }

    public AssemblyInfo? TryInspect(Stream stream)
    {
        CallCount++;
        return _result;
    }
}

internal sealed class CapturingAssemblyInspector : IAssemblyInspector
{
    public byte[]? Captured { get; private set; }

    public AssemblyInfo? TryInspect(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        Captured = copy.ToArray();
        return null;
    }
}

internal sealed class FakeFileSystem : IFileSystem
{
    private readonly string _rootPath;
    private readonly List<string> _orderedPaths = new();

    private record EntryData(
        bool IsDir,
        long Size,
        DateTimeOffset LastWrite,
        string Attributes,
        byte[] Content,
        bool ThrowOnRead = false);

    private readonly Dictionary<string, EntryData> _entries = new(StringComparer.OrdinalIgnoreCase);

    public FakeFileSystem(string rootPath) => _rootPath = rootPath;

    /// <summary>Adds a file using a relative path (backslashes OK — mimics Windows FS).</summary>
    public void AddFile(string relativePath, long size, DateTimeOffset lastWrite, string attributes, byte[] content)
    {
        var full = Path.Combine(_rootPath, relativePath);
        _orderedPaths.Add(full);
        _entries[full] = new EntryData(false, size, lastWrite, attributes, content);
    }

    /// <summary>Adds a file whose OpenRead will throw IOException.</summary>
    public void AddFileWithReadError(string relativePath)
    {
        var full = Path.Combine(_rootPath, relativePath);
        _orderedPaths.Add(full);
        _entries[full] = new EntryData(false, 0, DateTimeOffset.UtcNow, "Archive", Array.Empty<byte>(), ThrowOnRead: true);
    }

    public void AddDirectory(string relativePath)
    {
        var full = Path.Combine(_rootPath, relativePath);
        _orderedPaths.Add(full);
        _entries[full] = new EntryData(true, 0, DateTimeOffset.UtcNow, "Directory", Array.Empty<byte>());
    }

    // ---- IFileSystem ---------------------------------------------------------

    public IEnumerable<string> EnumerateFileSystemEntries(string rootPath, string searchPattern, SearchOption searchOption)
        => _orderedPaths;

    public bool IsDirectory(string path) => _entries.TryGetValue(path, out var e) && e.IsDir;

    /// <summary>Number of times <see cref="OpenRead"/> was called, across all paths.</summary>
    public int OpenReadCount { get; private set; }

    public Stream OpenRead(string path)
    {
        OpenReadCount++;
        var entry = _entries[path];
        if (entry.ThrowOnRead) throw new IOException($"Simulated read error for: {path}");
        return new MemoryStream(entry.Content);
    }

    public long GetFileSize(string path) => _entries[path].Size;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => _entries[path].LastWrite;

    public string GetAttributes(string path) => _entries[path].Attributes;
}

// ---------------------------------------------------------------------------
// DirectoryScannerTests
// ---------------------------------------------------------------------------

public class DirectoryScannerTests
{
    private const string RootPath = @"C:\scan-root";

    // SHA-256("hello") — precomputed, verified with PowerShell
    private const string HelloHash = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

    // SHA-256("test content") — precomputed, verified with PowerShell
    private const string TestContentHash = "6ae8a75555209fd6c44157c0aed8016e763ff435a19cf186f76863140143ff72";

    // -----------------------------------------------------------------------
    // 1. Empty directory
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_EmptyDirectory_ReturnsSnapshotWithNoEntries()
    {
        var fs = new FakeFileSystem(RootPath);
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries).IsEmpty();
        await Assert.That(snapshot.RootPath).IsEqualTo(RootPath);
    }

    // -----------------------------------------------------------------------
    // 2. Single file — correct metadata
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_SingleFile_ReturnsEntryWithCorrectMetadata()
    {
        var lastWrite = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var content = System.Text.Encoding.UTF8.GetBytes("hello");
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile(@"docs\readme.txt", 1024L, lastWrite, "Archive", content);
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries.Count).IsEqualTo(1);
        var entry = snapshot.Entries[0];
        await Assert.That(entry.RelativePath).IsEqualTo("docs/readme.txt");
        await Assert.That(entry.SizeBytes).IsEqualTo(1024L);
        await Assert.That(entry.LastModifiedUtc).IsEqualTo(lastWrite);
        await Assert.That(entry.IsDirectory).IsFalse();
        await Assert.That(entry.Hash).IsNotNull();
        await Assert.That(entry.Hash!.Length).IsEqualTo(64);
        await Assert.That(entry.Hash).Matches("^[0-9a-f]{64}$");
    }

    // -----------------------------------------------------------------------
    // 3. File hash is correct (known-content stream)
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_FileHashIsCorrect()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("test content");
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("file.txt", content.LongLength, DateTimeOffset.UtcNow, "Archive", content);
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries[0].Hash).IsEqualTo(TestContentHash);
    }

    // -----------------------------------------------------------------------
    // 4. Directory entry — no hash, no size
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_Directory_HasNoHashAndNoSize()
    {
        var fs = new FakeFileSystem(RootPath);
        fs.AddDirectory("subdir");
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries.Count).IsEqualTo(1);
        var entry = snapshot.Entries[0];
        await Assert.That(entry.IsDirectory).IsTrue();
        await Assert.That(entry.Hash).IsNull();
        await Assert.That(entry.SizeBytes).IsNull();
    }

    // -----------------------------------------------------------------------
    // 5. Entries are sorted by relative path
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_EntriesAreSortedByRelativePath()
    {
        var content = Array.Empty<byte>();
        var fs = new FakeFileSystem(RootPath);
        // Add in intentionally unsorted order
        fs.AddFile(@"z\file.txt", 0L, DateTimeOffset.UtcNow, "Archive", content);
        fs.AddFile(@"a\file.txt", 0L, DateTimeOffset.UtcNow, "Archive", content);
        fs.AddFile(@"m\file.txt", 0L, DateTimeOffset.UtcNow, "Archive", content);
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries.Count).IsEqualTo(3);
        await Assert.That(snapshot.Entries[0].RelativePath).IsEqualTo("a/file.txt");
        await Assert.That(snapshot.Entries[1].RelativePath).IsEqualTo("m/file.txt");
        await Assert.That(snapshot.Entries[2].RelativePath).IsEqualTo("z/file.txt");
    }

    // -----------------------------------------------------------------------
    // 6. Backslash paths are converted to forward slashes
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_BackslashPathsConvertedToForwardSlashes()
    {
        var fs = new FakeFileSystem(RootPath);
        // Path.Combine produces Windows backslash separators — exactly what PhysicalFileSystem returns
        fs.AddFile(@"windows\style\deep\path.txt", 0L, DateTimeOffset.UtcNow, "Archive", Array.Empty<byte>());
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        var relativePath = snapshot.Entries[0].RelativePath;
        await Assert.That(relativePath).IsEqualTo("windows/style/deep/path.txt");
        await Assert.That(relativePath.Contains('\\') || relativePath.Contains(@"\")).IsFalse();
    }

    // -----------------------------------------------------------------------
    // 7. Cancellation throws OperationCanceledException
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var fs = new FakeFileSystem(RootPath);
        var scanner = new DirectoryScanner(fs);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () => await scanner.ScanAsync(RootPath, cts.Token))
            .Throws<OperationCanceledException>();
    }

    // -----------------------------------------------------------------------
    // 8. Snapshot root path, timestamp and machine name are set
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_SnapshotRootPathAndTimestampAreSet()
    {
        var before = DateTimeOffset.UtcNow;
        var fs = new FakeFileSystem(RootPath);
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        var after = DateTimeOffset.UtcNow;
        await Assert.That(snapshot.RootPath).IsEqualTo(RootPath);
        await Assert.That(snapshot.CapturedAtUtc).IsGreaterThanOrEqualTo(before);
        await Assert.That(snapshot.CapturedAtUtc).IsLessThanOrEqualTo(after);
        await Assert.That(snapshot.MachineName).IsEqualTo(Environment.MachineName);
    }

    // -----------------------------------------------------------------------
    // 9. Mixed files and directories — all included with correct flags
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_MixedFilesAndDirectories_AllIncluded()
    {
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("file1.txt", 100L, DateTimeOffset.UtcNow, "Archive", new byte[] { 1 });
        fs.AddFile("file2.txt", 200L, DateTimeOffset.UtcNow, "Archive", new byte[] { 2 });
        fs.AddDirectory("emptydir");
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries.Count).IsEqualTo(3);

        var files = snapshot.Entries.Where(e => !e.IsDirectory).ToList();
        var dirs = snapshot.Entries.Where(e => e.IsDirectory).ToList();
        await Assert.That(files.Count).IsEqualTo(2);
        await Assert.That(dirs.Count).IsEqualTo(1);
    }

    // -----------------------------------------------------------------------
    // 10. File read error — entry still included, Hash and SizeBytes are null
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_FileReadError_SkipsEntryWithNullHash()
    {
        var fs = new FakeFileSystem(RootPath);
        fs.AddFileWithReadError("locked.txt");
        var scanner = new DirectoryScanner(fs);

        var snapshot = await scanner.ScanAsync(RootPath);

        // Entry should still appear — scanner tolerates unreadable files
        await Assert.That(snapshot.Entries.Count).IsEqualTo(1);
        await Assert.That(snapshot.Entries[0].Hash).IsNull();
        // Metadata that could be read is retained so the file is not mistaken for a changed one
        await Assert.That(snapshot.Entries[0].SizeBytes).IsNotNull();
        await Assert.That(snapshot.Entries[0].LastModifiedUtc).IsNotEqualTo(DateTimeOffset.MinValue);
        await Assert.That(snapshot.Entries[0].Attributes).IsNotNull();
    }

    // -----------------------------------------------------------------------
    // 11. Assembly inspection — DLL with inspector populates AssemblyInfo
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_DllWithInspector_PopulatesAssemblyInfo()
    {
        var expectedInfo = new AssemblyInfo { Name = "TestLib", Version = "2.0.0.0", PublicTypes = [] };
        var inspector = new FakeAssemblyInspector(expectedInfo);
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("lib.dll", 4096L, DateTimeOffset.UtcNow, "Archive", new byte[] { 1, 2, 3, 4 });
        var scanner = new DirectoryScanner(fs, inspector);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries[0].AssemblyInfo).IsNotNull();
        await Assert.That(snapshot.Entries[0].AssemblyInfo!.Name).IsEqualTo("TestLib");
        await Assert.That(inspector.CallCount).IsEqualTo(1);
    }

    // -----------------------------------------------------------------------
    // 12. Assembly inspection — .exe also triggers inspector
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_ExeWithInspector_PopulatesAssemblyInfo()
    {
        var expectedInfo = new AssemblyInfo { Name = "TestApp", Version = "1.0.0.0", PublicTypes = [] };
        var inspector = new FakeAssemblyInspector(expectedInfo);
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("app.exe", 8192L, DateTimeOffset.UtcNow, "Archive", new byte[] { 1, 2, 3, 4 });
        var scanner = new DirectoryScanner(fs, inspector);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries[0].AssemblyInfo).IsNotNull();
    }

    // -----------------------------------------------------------------------
    // 13. Assembly inspection — non-.NET binary inspector returns null
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_DllWithInspector_ReadsFileOnlyOnce()
    {
        var inspector = new FakeAssemblyInspector(new AssemblyInfo { Name = "TestLib", Version = "1.0.0.0", PublicTypes = [] });
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("lib.dll", 4, DateTimeOffset.UtcNow, "Archive", new byte[] { 1, 2, 3, 4 });
        var scanner = new DirectoryScanner(fs, inspector);

        await scanner.ScanAsync(RootPath);

        await Assert.That(fs.OpenReadCount).IsEqualTo(1);
        await Assert.That(inspector.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ScanAsync_DllWithInspector_InspectorGetsWholeFileFromStartAndHashIsUnaffected()
    {
        var content = "assembly bytes"u8.ToArray();
        var inspector = new CapturingAssemblyInspector();
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("lib.dll", content.Length, DateTimeOffset.UtcNow, "Archive", content);
        var scanner = new DirectoryScanner(fs, inspector);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(inspector.Captured).IsNotNull();
        await Assert.That(inspector.Captured!.SequenceEqual(content)).IsTrue();
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
        await Assert.That(snapshot.Entries[0].Hash).IsEqualTo(expectedHash);
    }

    [Test]
    public async Task ScanAsync_DllWithInspector_ReadErrorLeavesHashAndAssemblyInfoNull()
    {
        var inspector = new FakeAssemblyInspector(new AssemblyInfo { Name = "TestLib", Version = "1.0.0.0", PublicTypes = [] });
        var fs = new FakeFileSystem(RootPath);
        fs.AddFileWithReadError("locked.dll");
        var scanner = new DirectoryScanner(fs, inspector);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries[0].Hash).IsNull();
        await Assert.That(snapshot.Entries[0].AssemblyInfo).IsNull();
        await Assert.That(inspector.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task ScanAsync_DllWhereInspectorReturnsNull_AssemblyInfoIsNull()
    {
        var inspector = new FakeAssemblyInspector(null);
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("native.dll", 1024L, DateTimeOffset.UtcNow, "Archive", new byte[] { 1, 2, 3 });
        var scanner = new DirectoryScanner(fs, inspector);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries[0].AssemblyInfo).IsNull();
        await Assert.That(inspector.CallCount).IsEqualTo(1);
    }

    // -----------------------------------------------------------------------
    // 14. Assembly inspection — non-binary files do not invoke inspector
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_TextFileWithInspector_InspectorNotCalled()
    {
        var inspector = new FakeAssemblyInspector(null);
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("readme.txt", 100L, DateTimeOffset.UtcNow, "Archive", new byte[] { 1 });
        var scanner = new DirectoryScanner(fs, inspector);

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(inspector.CallCount).IsEqualTo(0);
        await Assert.That(snapshot.Entries[0].AssemblyInfo).IsNull();
    }

    // -----------------------------------------------------------------------
    // 15. Assembly inspection — no inspector means AssemblyInfo is always null
    // -----------------------------------------------------------------------

    [Test]
    public async Task ScanAsync_DllWithoutInspector_AssemblyInfoIsNull()
    {
        var fs = new FakeFileSystem(RootPath);
        fs.AddFile("lib.dll", 1024L, DateTimeOffset.UtcNow, "Archive", new byte[] { 1, 2, 3 });
        var scanner = new DirectoryScanner(fs); // no inspector

        var snapshot = await scanner.ScanAsync(RootPath);

        await Assert.That(snapshot.Entries[0].AssemblyInfo).IsNull();
    }
}
