using System.Diagnostics;
using System.Security.Cryptography;
using DiffTool.Core.Models;

namespace DiffTool.Core.Scanning;

/// <summary>
/// Scans a directory tree and produces a <see cref="DirectorySnapshot"/>.
/// Uses <see cref="IFileSystem"/> abstraction for testability.
/// </summary>
public sealed class DirectoryScanner
{
    private readonly IFileSystem _fileSystem;
    private readonly IAssemblyInspector? _assemblyInspector;

    public DirectoryScanner(IFileSystem fileSystem, IAssemblyInspector? assemblyInspector = null)
    {
        _fileSystem = fileSystem;
        _assemblyInspector = assemblyInspector;
    }

    /// <summary>
    /// Scans the given directory and returns a snapshot of its current state.
    /// </summary>
    /// <param name="rootPath">Absolute path to the directory to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A snapshot representing the directory tree at this moment.</returns>
    public async Task<DirectorySnapshot> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        rootPath = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        cancellationToken.ThrowIfCancellationRequested();

        var entries = new List<FileEntry>();

        foreach (var path in _fileSystem.EnumerateFileSystemEntries(rootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = path[rootPath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                                   .Replace('\\', '/');

            if (_fileSystem.IsDirectory(path))
            {
                entries.Add(new FileEntry
                {
                    RelativePath = relative,
                    IsDirectory = true,
                    LastModifiedUtc = _fileSystem.GetLastWriteTimeUtc(path),
                    Attributes = _fileSystem.GetAttributes(path),
                });
            }
            else
            {
                long? size = null;
                string? hash = null;
                DateTimeOffset lastModified;
                string? attributes = null;
                AssemblyInfo? assemblyInfo = null;

                try
                {
                    size = _fileSystem.GetFileSize(path);
                    lastModified = _fileSystem.GetLastWriteTimeUtc(path);
                    attributes = _fileSystem.GetAttributes(path);

                    await using var stream = _fileSystem.OpenRead(path);
                    var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                    hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine($"[DirectoryScanner] Skipping '{path}': {ex.Message}");
                    lastModified = DateTimeOffset.MinValue;
                    size = null;
                }

                if (_assemblyInspector != null && hash != null && IsAssemblyFile(path))
                {
                    try
                    {
                        await using var assemblyStream = _fileSystem.OpenRead(path);
                        assemblyInfo = _assemblyInspector.TryInspect(assemblyStream);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        Debug.WriteLine($"[DirectoryScanner] Could not inspect assembly '{path}': {ex.Message}");
                    }
                }

                entries.Add(new FileEntry
                {
                    RelativePath = relative,
                    IsDirectory = false,
                    SizeBytes = size,
                    Hash = hash,
                    LastModifiedUtc = lastModified,
                    Attributes = attributes,
                    AssemblyInfo = assemblyInfo,
                });
            }
        }

        entries.Sort(static (a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.Ordinal));

        return new DirectorySnapshot
        {
            RootPath = rootPath,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Entries = entries,
            MachineName = Environment.MachineName,
        };
    }

    private static bool IsAssemblyFile(string path)
    {
        var ext = Path.GetExtension(path.AsSpan());
        return ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }
}
