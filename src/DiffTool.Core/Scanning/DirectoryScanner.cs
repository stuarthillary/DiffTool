using DiffTool.Core.Models;

namespace DiffTool.Core.Scanning;

/// <summary>
/// Scans a directory tree and produces a <see cref="DirectorySnapshot"/>.
/// Uses <see cref="IFileSystem"/> abstraction for testability.
/// </summary>
public sealed class DirectoryScanner
{
    private readonly IFileSystem _fileSystem;

    public DirectoryScanner(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Scans the given directory and returns a snapshot of its current state.
    /// </summary>
    /// <param name="rootPath">Absolute path to the directory to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A snapshot representing the directory tree at this moment.</returns>
    public Task<DirectorySnapshot> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
