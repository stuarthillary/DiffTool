namespace DiffTool.Core.Scanning;

/// <summary>
/// Abstraction over filesystem operations for testability.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Enumerates all files and directories under the given root path, recursively.
    /// </summary>
    IEnumerable<string> EnumerateFileSystemEntries(string rootPath, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Returns true if the path is a directory.
    /// </summary>
    bool IsDirectory(string path);

    /// <summary>
    /// Opens a read-only stream for the given file path.
    /// </summary>
    Stream OpenRead(string path);

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    long GetFileSize(string path);

    /// <summary>
    /// Gets the last write time in UTC.
    /// </summary>
    DateTimeOffset GetLastWriteTimeUtc(string path);

    /// <summary>
    /// Gets file attributes as a descriptive string.
    /// </summary>
    string GetAttributes(string path);
}
