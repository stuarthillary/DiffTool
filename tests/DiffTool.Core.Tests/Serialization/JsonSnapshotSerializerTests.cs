using System.Text;
using System.Text.Json;
using DiffTool.Core.Models;
using DiffTool.Core.Serialization;

namespace DiffTool.Core.Tests.Serialization;

public class JsonSnapshotSerializerTests
{
    private static readonly DateTimeOffset Ts = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private static DirectorySnapshot MakeSnapshot(string rootPath = @"C:\data", string? machineName = "testpc") =>
        new()
        {
            RootPath = rootPath,
            CapturedAtUtc = Ts,
            MachineName = machineName,
            Version = 1,
            Entries = new List<FileEntry>
            {
                new()
                {
                    RelativePath = "docs/readme.txt",
                    SizeBytes = 1024L,
                    Hash = "abc123def456",
                    LastModifiedUtc = Ts,
                    IsDirectory = false,
                    Attributes = "Archive"
                },
                new()
                {
                    RelativePath = "images",
                    LastModifiedUtc = Ts,
                    IsDirectory = true,
                    Attributes = "Directory"
                }
            }
        };

    // -----------------------------------------------------------------------
    // 1. FileExtension returns ".json"
    // -----------------------------------------------------------------------

    [Test]
    public async Task FileExtension_IsJson()
    {
        var serializer = new JsonSnapshotSerializer();

        await Assert.That(serializer.FileExtension).IsEqualTo(".json");
    }

    // -----------------------------------------------------------------------
    // 2. Full round-trip preserves all fields
    // -----------------------------------------------------------------------

    [Test]
    public async Task SerializeDeserialize_RoundTrip_PreservesAllFields()
    {
        var original = MakeSnapshot();
        var serializer = new JsonSnapshotSerializer();

        using var ms = new MemoryStream();
        await serializer.SerializeAsync(original, ms);
        ms.Position = 0;
        var result = await serializer.DeserializeAsync(ms);

        await Assert.That(result.RootPath).IsEqualTo(original.RootPath);
        await Assert.That(result.CapturedAtUtc).IsEqualTo(original.CapturedAtUtc);
        await Assert.That(result.Version).IsEqualTo(original.Version);
        await Assert.That(result.MachineName).IsEqualTo(original.MachineName);
        await Assert.That(result.Entries.Count).IsEqualTo(2);

        var file = result.Entries.First(e => !e.IsDirectory);
        await Assert.That(file.RelativePath).IsEqualTo("docs/readme.txt");
        await Assert.That(file.SizeBytes).IsEqualTo(1024L);
        await Assert.That(file.Hash).IsEqualTo("abc123def456");
        await Assert.That(file.LastModifiedUtc).IsEqualTo(Ts);
        await Assert.That(file.IsDirectory).IsFalse();
        await Assert.That(file.Attributes).IsEqualTo("Archive");

        var dir = result.Entries.First(e => e.IsDirectory);
        await Assert.That(dir.RelativePath).IsEqualTo("images");
        await Assert.That(dir.IsDirectory).IsTrue();
        await Assert.That(dir.Attributes).IsEqualTo("Directory");
    }

    // -----------------------------------------------------------------------
    // 3. Empty entries round-trips
    // -----------------------------------------------------------------------

    [Test]
    public async Task SerializeDeserialize_EmptyEntries_RoundTrips()
    {
        var original = new DirectorySnapshot
        {
            RootPath = @"C:\empty",
            CapturedAtUtc = Ts,
            Entries = new List<FileEntry>()
        };
        var serializer = new JsonSnapshotSerializer();

        using var ms = new MemoryStream();
        await serializer.SerializeAsync(original, ms);
        ms.Position = 0;
        var result = await serializer.DeserializeAsync(ms);

        await Assert.That(result.Entries).IsEmpty();
        await Assert.That(result.RootPath).IsEqualTo(@"C:\empty");
    }

    // -----------------------------------------------------------------------
    // 4. Produces valid JSON with camelCase field names
    // -----------------------------------------------------------------------

    [Test]
    public async Task Serialize_ProducesValidJson()
    {
        var snapshot = MakeSnapshot();
        var serializer = new JsonSnapshotSerializer();

        using var ms = new MemoryStream();
        await serializer.SerializeAsync(snapshot, ms);
        ms.Position = 0;
        var json = Encoding.UTF8.GetString(ms.ToArray());

        // Must be parseable as JSON
        using var doc = JsonDocument.Parse(json);

        // Field names must be camelCase
        await Assert.That(json).Contains("rootPath");
        await Assert.That(json).Contains("capturedAtUtc");
        await Assert.That(json).Contains("entries");
        await Assert.That(json).Contains("relativePath");
        await Assert.That(json).Contains("lastModifiedUtc");
    }

    // -----------------------------------------------------------------------
    // 5. Null fields omitted from JSON (WhenWritingNull)
    // -----------------------------------------------------------------------

    [Test]
    public async Task Serialize_NullableFields_OmittedWhenNull()
    {
        var snapshot = new DirectorySnapshot
        {
            RootPath = @"C:\test",
            CapturedAtUtc = Ts,
            MachineName = null,
            Entries = new List<FileEntry>
            {
                new()
                {
                    RelativePath = "file.txt",
                    LastModifiedUtc = Ts,
                    IsDirectory = false,
                    // Hash, SizeBytes, Attributes intentionally left null
                }
            }
        };
        var serializer = new JsonSnapshotSerializer();

        using var ms = new MemoryStream();
        await serializer.SerializeAsync(snapshot, ms);
        var json = Encoding.UTF8.GetString(ms.ToArray());

        await Assert.That(json.Contains("\"hash\"")).IsFalse();
        await Assert.That(json.Contains("\"sizeBytes\"")).IsFalse();
        await Assert.That(json.Contains("\"attributes\"")).IsFalse();
        await Assert.That(json.Contains("\"machineName\"")).IsFalse();
    }

    // -----------------------------------------------------------------------
    // 6. Empty stream throws InvalidDataException
    // -----------------------------------------------------------------------

    [Test]
    public async Task Deserialize_EmptyStream_ThrowsInvalidDataException()
    {
        var serializer = new JsonSnapshotSerializer();
        using var ms = new MemoryStream();

        await Assert.That(async () => await serializer.DeserializeAsync(ms))
            .Throws<InvalidDataException>();
    }

    // -----------------------------------------------------------------------
    // 7. Invalid JSON throws InvalidDataException
    // -----------------------------------------------------------------------

    [Test]
    public async Task Deserialize_InvalidJson_ThrowsInvalidDataException()
    {
        var serializer = new JsonSnapshotSerializer();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("not valid json"));

        await Assert.That(async () => await serializer.DeserializeAsync(ms))
            .Throws<InvalidDataException>();
    }

    // -----------------------------------------------------------------------
    // 8. Null MachineName survives round-trip
    // -----------------------------------------------------------------------

    [Test]
    public async Task SerializeDeserialize_NullMachineName_RoundTrips()
    {
        var original = MakeSnapshot(machineName: null);
        var serializer = new JsonSnapshotSerializer();

        using var ms = new MemoryStream();
        await serializer.SerializeAsync(original, ms);
        ms.Position = 0;
        var result = await serializer.DeserializeAsync(ms);

        await Assert.That(result.MachineName).IsNull();
    }

    // -----------------------------------------------------------------------
    // 9. Non-null MachineName survives round-trip
    // -----------------------------------------------------------------------

    [Test]
    public async Task SerializeDeserialize_NonNullMachineName_RoundTrips()
    {
        var original = MakeSnapshot(machineName: "mypc");
        var serializer = new JsonSnapshotSerializer();

        using var ms = new MemoryStream();
        await serializer.SerializeAsync(original, ms);
        ms.Position = 0;
        var result = await serializer.DeserializeAsync(ms);

        await Assert.That(result.MachineName).IsEqualTo("mypc");
    }
}
