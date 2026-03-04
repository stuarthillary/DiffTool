namespace DiffTool.Core.Scanning;

/// <summary>
/// Real filesystem implementation of <see cref="IFileSystem"/>.
/// Delegates to System.IO for all operations.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    /// <inheritdoc />
    public IEnumerable<string> EnumerateFileSystemEntries(string rootPath, string searchPattern, SearchOption searchOption)
        => Directory.EnumerateFileSystemEntries(rootPath, searchPattern, searchOption);

    /// <inheritdoc />
    public bool IsDirectory(string path)
        => (File.GetAttributes(path) & FileAttributes.Directory) != 0;

    /// <inheritdoc />
    public Stream OpenRead(string path)
        => File.OpenRead(path);

    /// <inheritdoc />
    public long GetFileSize(string path)
        => new FileInfo(path).Length;

    /// <inheritdoc />
    public DateTimeOffset GetLastWriteTimeUtc(string path)
        => new DateTimeOffset(new FileInfo(path).LastWriteTimeUtc, TimeSpan.Zero);

    /// <inheritdoc />
    public string GetAttributes(string path)
        => File.GetAttributes(path).ToString();
}
