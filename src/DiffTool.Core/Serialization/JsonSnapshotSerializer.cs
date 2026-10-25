using System.Text.Json;
using DiffTool.Core.Models;

namespace DiffTool.Core.Serialization;

/// <summary>
/// JSON serializer for snapshots using System.Text.Json.
/// Produces indented, deterministic output that is git-diffable.
/// </summary>
public sealed class JsonSnapshotSerializer : ISnapshotSerializer
{
    /// <inheritdoc />
    public string FileExtension => ".json";

    /// <inheritdoc />
    public Task SerializeAsync(DirectorySnapshot snapshot, Stream output, CancellationToken cancellationToken = default)
        => JsonSerializer.SerializeAsync(output, snapshot, SnapshotJsonContext.Default.DirectorySnapshot, cancellationToken);

    /// <inheritdoc />
    public async Task<DirectorySnapshot> DeserializeAsync(Stream input, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await JsonSerializer.DeserializeAsync(input, SnapshotJsonContext.Default.DirectorySnapshot, cancellationToken);
            if (result is null)
                throw new InvalidDataException("Stream did not contain a valid DirectorySnapshot.");
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Stream did not contain valid JSON.", ex);
        }
    }
}
