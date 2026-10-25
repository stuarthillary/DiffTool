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
                var lastModified = DateTimeOffset.MinValue;
                string? attributes = null;
                AssemblyInfo? assemblyInfo = null;

                // Metadata is captured separately from the hash so an unreadable file keeps
                // whatever metadata is available and does not look changed against a readable copy.
                try
                {
                    size = _fileSystem.GetFileSize(path);
                    lastModified = _fileSystem.GetLastWriteTimeUtc(path);
                    attributes = _fileSystem.GetAttributes(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine($"[DirectoryScanner] Could not read metadata for '{path}': {ex.Message}");
                }

                // Assemblies are buffered so the file is read from disk once for both hashing and
                // inspection; everything else is streamed straight into the hash.
                var inspect = _assemblyInspector != null && IsAssemblyFile(path);

                try
                {
                    await using var stream = _fileSystem.OpenRead(path);
                    if (inspect)
                    {
                        var buffer = new MemoryStream();
                        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                        buffer.Position = 0;
                        hash = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
                        buffer.Position = 0;
                        assemblyInfo = _assemblyInspector!.TryInspect(buffer);
                    }
                    else
                    {
                        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                        hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine($"[DirectoryScanner] Skipping hash for '{path}': {ex.Message}");
                    hash = null;
                    assemblyInfo = null;
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
