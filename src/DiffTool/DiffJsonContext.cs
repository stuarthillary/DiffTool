using System.Text.Json.Serialization;
using DiffTool.Core.Models;

namespace DiffTool;

/// <summary>
/// Source-generated JSON metadata for diff files, so the diff and report commands need no runtime reflection (trim/AOT safe).
/// Unlike snapshots, diffs are written with explicit nulls.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SnapshotDiff))]
internal sealed partial class DiffJsonContext : JsonSerializerContext;
