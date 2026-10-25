using System.Text.Json.Serialization;
using DiffTool.Core.Models;

namespace DiffTool.Core.Serialization;

/// <summary>
/// Source-generated JSON metadata for snapshots, so serialization needs no runtime reflection (trim/AOT safe).
/// Mirrors the options of <see cref="JsonSnapshotSerializer"/>: indented, camelCase, nulls omitted.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DirectorySnapshot))]
internal sealed partial class SnapshotJsonContext : JsonSerializerContext;
