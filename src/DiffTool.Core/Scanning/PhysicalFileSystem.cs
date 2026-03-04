namespace DiffTool.Core.Scanning;

/// <summary>
/// Real filesystem implementation of <see cref="IFileSystem"/>.
/// Delegates to System.IO for all operations.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    /// <inheritdoc />
    public IEnumerable<string> EnumerateFileSystemEntries(string rootPath, string searchPattern, SearchOption searchOption)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public bool IsDirectory(string path)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Stream OpenRead(string path)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public long GetFileSize(string path)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public DateTimeOffset GetLastWriteTimeUtc(string path)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public string GetAttributes(string path)
    {
        throw new NotImplementedException();
    }
}
