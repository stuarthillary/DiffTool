using System.IO.Enumeration;

namespace DiffTool.Core.Scanning;

/// <summary>
/// Real filesystem implementation of <see cref="IFileSystem"/>.
/// Delegates to System.IO for all operations.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    /// <remarks>
    /// Directory symlinks and junctions (reparse points) are returned as entries but never descended into,
    /// so a link cycle cannot make a scan loop forever and linked trees are not counted twice.
    /// Directories that cannot be read are skipped rather than aborting the whole enumeration.
    /// </remarks>
    /// <inheritdoc />
    public IEnumerable<string> EnumerateFileSystemEntries(string rootPath, string searchPattern, SearchOption searchOption)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
            AttributesToSkip = 0,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };

        return new FileSystemEnumerable<string>(
            rootPath,
            static (ref FileSystemEntry entry) => entry.ToFullPath(),
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                FileSystemName.MatchesSimpleExpression(searchPattern, entry.FileName),
            ShouldRecursePredicate = static (ref FileSystemEntry entry) =>
                entry.IsDirectory && (entry.Attributes & FileAttributes.ReparsePoint) == 0,
        };
    }

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
