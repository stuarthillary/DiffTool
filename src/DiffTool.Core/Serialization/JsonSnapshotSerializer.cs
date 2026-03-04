using System.Text.Json;
using DiffTool.Core.Models;

namespace DiffTool.Core.Serialization;

/// <summary>
/// JSON serializer for snapshots using System.Text.Json.
/// Produces indented, deterministic output that is git-diffable.
/// </summary>
public sealed class JsonSnapshotSerializer : ISnapshotSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public string FileExtension => ".json";

    /// <inheritdoc />
    public Task SerializeAsync(DirectorySnapshot snapshot, Stream output, CancellationToken cancellationToken = default)
        => JsonSerializer.SerializeAsync(output, snapshot, s_options, cancellationToken);

    /// <inheritdoc />
    public async Task<DirectorySnapshot> DeserializeAsync(Stream input, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await JsonSerializer.DeserializeAsync<DirectorySnapshot>(input, s_options, cancellationToken);
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
