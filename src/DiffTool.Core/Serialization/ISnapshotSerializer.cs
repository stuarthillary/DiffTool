using DiffTool.Core.Models;

namespace DiffTool.Core.Serialization;

/// <summary>
/// Abstraction for snapshot serialization/deserialization.
/// Allows swapping formats (JSON, YAML, etc.) without changing consumers.
/// </summary>
public interface ISnapshotSerializer
{
    /// <summary>
    /// Serializes a snapshot to a stream.
    /// </summary>
    Task SerializeAsync(DirectorySnapshot snapshot, Stream output, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes a snapshot from a stream.
    /// </summary>
    Task<DirectorySnapshot> DeserializeAsync(Stream input, CancellationToken cancellationToken = default);

    /// <summary>
    /// The file extension associated with this format (e.g., ".json").
    /// </summary>
    string FileExtension { get; }
}
