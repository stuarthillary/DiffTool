using System.Diagnostics;
using DiffTool.Core.Scanning;

namespace DiffTool.Core.Tests.Scanning;

/// <summary>
/// Exercises <see cref="PhysicalFileSystem"/> and a real-disk scan against a temporary directory.
/// </summary>
public class PhysicalFileSystemTests
{
    private sealed class TempTree : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "DiffToolFsTests-" + Guid.NewGuid().ToString("N"));

        public TempTree() => Directory.CreateDirectory(Root);

        public string Path_(string relative) => Path.Combine(Root, relative);

        public string WriteFile(string relative, string content)
        {
            var full = Path_(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// Creates a directory link (symlink, falling back to an NTFS junction on Windows).
    /// Returns false when the platform or privileges do not allow either.
    /// </summary>
    private static bool TryCreateDirectoryLink(string linkPath, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            if (!OperatingSystem.IsWindows())
                return false;
        }

        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{target}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi);
        if (process is null)
            return false;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 && Directory.Exists(linkPath);
    }

    private static List<string> Enumerate(string root, string pattern = "*", SearchOption option = SearchOption.AllDirectories)
        => new PhysicalFileSystem().EnumerateFileSystemEntries(root, pattern, option)
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

    // ---- enumeration ---------------------------------------------------------

    [Test]
    public async Task Enumerate_AllDirectories_ReturnsFilesAndDirectoriesRecursively()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.txt", "a");
        tree.WriteFile("sub/b.txt", "b");
        tree.WriteFile("sub/deep/c.txt", "c");

        var entries = Enumerate(tree.Root);

        await Assert.That(entries).IsEquivalentTo(new[] { "a.txt", "sub", "sub/b.txt", "sub/deep", "sub/deep/c.txt" });
    }

    [Test]
    public async Task Enumerate_TopDirectoryOnly_DoesNotRecurse()
    {
        using var tree = new TempTree();
        tree.WriteFile("a.txt", "a");
        tree.WriteFile("sub/b.txt", "b");

        var entries = Enumerate(tree.Root, "*", SearchOption.TopDirectoryOnly);

        await Assert.That(entries).IsEquivalentTo(new[] { "a.txt", "sub" });
    }

    [Test]
    public async Task Enumerate_SearchPattern_FiltersByName()
    {
        using var tree = new TempTree();
        tree.WriteFile("keep.txt", "a");
        tree.WriteFile("skip.log", "b");
        tree.WriteFile("sub/also.txt", "c");

        var entries = Enumerate(tree.Root, "*.txt");

        await Assert.That(entries).IsEquivalentTo(new[] { "keep.txt", "sub/also.txt" });
    }

    [Test]
    public async Task Enumerate_EmptyDirectory_ReturnsNothing()
    {
        using var tree = new TempTree();

        await Assert.That(Enumerate(tree.Root).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Enumerate_DirectoryLink_IsListedButNotDescendedInto()
    {
        using var tree = new TempTree();
        tree.WriteFile("real/file.txt", "x");
        if (!TryCreateDirectoryLink(tree.Path_("link"), tree.Path_("real")))
        {
            Skip.Test("Directory links cannot be created in this environment.");
            return;
        }

        var entries = Enumerate(tree.Root);

        await Assert.That(entries).Contains("link");
        await Assert.That(entries).Contains("real/file.txt");
        await Assert.That(entries).DoesNotContain("link/file.txt");
    }

    [Test]
    public async Task Enumerate_DirectoryLinkCycle_Terminates()
    {
        using var tree = new TempTree();
        tree.WriteFile("dir/file.txt", "x");
        if (!TryCreateDirectoryLink(tree.Path_("dir/loop"), tree.Root))
        {
            Skip.Test("Directory links cannot be created in this environment.");
            return;
        }

        var entries = await Task.Run(() => Enumerate(tree.Root)).WaitAsync(TimeSpan.FromSeconds(30));

        await Assert.That(entries).IsEquivalentTo(new[] { "dir", "dir/file.txt", "dir/loop" });
    }

    // ---- per-path operations -------------------------------------------------

    [Test]
    public async Task IsDirectory_DistinguishesFilesFromDirectories()
    {
        using var tree = new TempTree();
        var file = tree.WriteFile("a.txt", "a");
        var fs = new PhysicalFileSystem();

        await Assert.That(fs.IsDirectory(file)).IsFalse();
        await Assert.That(fs.IsDirectory(tree.Root)).IsTrue();
    }

    [Test]
    public async Task OpenRead_ReturnsFileContent()
    {
        using var tree = new TempTree();
        var file = tree.WriteFile("a.txt", "hello");
        var fs = new PhysicalFileSystem();

        await using var stream = fs.OpenRead(file);
        using var reader = new StreamReader(stream);

        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("hello");
    }

    [Test]
    public async Task OpenRead_MissingFile_Throws()
    {
        using var tree = new TempTree();
        var fs = new PhysicalFileSystem();

        await Assert.That(() => fs.OpenRead(tree.Path_("missing.txt"))).Throws<FileNotFoundException>();
    }

    [Test]
    public async Task GetFileSize_ReturnsByteLength()
    {
        using var tree = new TempTree();
        var file = tree.WriteFile("a.txt", "12345");

        await Assert.That(new PhysicalFileSystem().GetFileSize(file)).IsEqualTo(5L);
    }

    [Test]
    public async Task GetLastWriteTimeUtc_ReturnsUtcValueWithZeroOffset()
    {
        using var tree = new TempTree();
        var file = tree.WriteFile("a.txt", "a");
        var stamp = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, stamp);

        var result = new PhysicalFileSystem().GetLastWriteTimeUtc(file);

        await Assert.That(result.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(result.UtcDateTime).IsEqualTo(stamp);
    }

    [Test]
    public async Task GetAttributes_ReportsFileAndDirectoryFlags()
    {
        using var tree = new TempTree();
        var file = tree.WriteFile("a.txt", "a");
        var fs = new PhysicalFileSystem();

        await Assert.That(fs.GetAttributes(tree.Root)).Contains("Directory");
        await Assert.That(fs.GetAttributes(file)).DoesNotContain("Directory");
    }

    [Test]
    public async Task GetAttributes_ReflectsReadOnlyFlag()
    {
        using var tree = new TempTree();
        var file = tree.WriteFile("a.txt", "a");
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
        try
        {
            await Assert.That(new PhysicalFileSystem().GetAttributes(file)).Contains("ReadOnly");
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    // ---- real-disk scan ------------------------------------------------------

    [Test]
    public async Task Scan_RealDirectory_ProducesSortedForwardSlashPathsAndCorrectHashes()
    {
        using var tree = new TempTree();
        tree.WriteFile("b.txt", "hello");
        tree.WriteFile("sub/a.txt", "test content");
        var scanner = new DirectoryScanner(new PhysicalFileSystem());

        // A trailing separator on the root must not leak into relative paths.
        var snapshot = await scanner.ScanAsync(tree.Root + Path.DirectorySeparatorChar);

        await Assert.That(snapshot.Entries.Select(e => e.RelativePath).ToList())
            .IsEquivalentTo(new[] { "b.txt", "sub", "sub/a.txt" });
        await Assert.That(snapshot.Entries.Select(e => e.RelativePath).SequenceEqual(
            snapshot.Entries.Select(e => e.RelativePath).Order(StringComparer.Ordinal))).IsTrue();
        await Assert.That(snapshot.Entries.Single(e => e.RelativePath == "b.txt").Hash)
            .IsEqualTo("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
        await Assert.That(snapshot.Entries.Single(e => e.RelativePath == "sub").IsDirectory).IsTrue();
        await Assert.That(snapshot.Entries.Single(e => e.RelativePath == "sub/a.txt").SizeBytes).IsEqualTo(12L);
    }

    [Test]
    public async Task Scan_RealDirectoryWithAssembly_InspectsAndHashesFromOneRead()
    {
        using var tree = new TempTree();
        var dll = tree.Path_("lib.dll");
        File.Copy(typeof(DirectoryScanner).Assembly.Location, dll);
        var scanner = new DirectoryScanner(new PhysicalFileSystem(), new AssemblyInspector());

        var snapshot = await scanner.ScanAsync(tree.Root);

        var entry = snapshot.Entries.Single();
        await Assert.That(entry.AssemblyInfo).IsNotNull();
        await Assert.That(entry.AssemblyInfo!.Name).IsEqualTo("DiffTool.Core");
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dll))).ToLowerInvariant();
        await Assert.That(entry.Hash).IsEqualTo(expected);
    }
}
