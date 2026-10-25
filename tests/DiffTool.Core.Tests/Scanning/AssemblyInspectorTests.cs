using DiffTool.Core.Models;
using DiffTool.Core.Scanning;

namespace DiffTool.Core.Tests.Scanning;

/// <summary>
/// Integration tests for AssemblyInspector using the DiffTool.Core assembly itself as a known test subject.
/// </summary>
public class AssemblyInspectorTests
{
    // DiffTool.Core.dll is always present in the output directory alongside this test executable.
    private static readonly string CoreAssemblyPath = typeof(FileEntry).Assembly.Location;

    // -----------------------------------------------------------------------
    // 1. Valid assembly — top-level result
    // -----------------------------------------------------------------------

    [Test]
    public async Task TryInspect_ValidAssembly_ReturnsNonNull()
    {
        var inspector = new AssemblyInspector();
        await using var stream = File.OpenRead(CoreAssemblyPath);

        var result = inspector.TryInspect(stream);

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task TryInspect_ValidAssembly_NameAndVersionAreSet()
    {
        var inspector = new AssemblyInspector();
        await using var stream = File.OpenRead(CoreAssemblyPath);

        var result = inspector.TryInspect(stream)!;

        await Assert.That(result.Name).IsEqualTo("DiffTool.Core");
        await Assert.That(result.Version).Matches(@"^\d+\.\d+\.\d+\.\d+$");
    }

    [Test]
    public async Task TryInspect_ValidAssembly_HasPublicTypes()
    {
        var inspector = new AssemblyInspector();
        await using var stream = File.OpenRead(CoreAssemblyPath);

        var result = inspector.TryInspect(stream)!;

        await Assert.That(result.PublicTypes).IsNotEmpty();
    }

    [Test]
    public async Task TryInspect_ValidAssembly_TypesAreSortedByFullName()
    {
        var inspector = new AssemblyInspector();
        await using var stream = File.OpenRead(CoreAssemblyPath);

        var result = inspector.TryInspect(stream)!;
        var names = result.PublicTypes.Select(t => t.FullName).ToList();

        await Assert.That(names.SequenceEqual(names.OrderBy(x => x, StringComparer.Ordinal))).IsTrue();
    }

    // -----------------------------------------------------------------------
    // 2. Known type — FileEntry
    // -----------------------------------------------------------------------

    [Test]
    public async Task TryInspect_ValidAssembly_FindsFileEntryType()
    {
        var inspector = new AssemblyInspector();
        await using var stream = File.OpenRead(CoreAssemblyPath);

        var result = inspector.TryInspect(stream)!;
        var fileEntry = result.PublicTypes.FirstOrDefault(t => t.FullName == "DiffTool.Core.Models.FileEntry");

        await Assert.That(fileEntry).IsNotNull();
        await Assert.That(fileEntry!.Kind).IsEqualTo("class");
        await Assert.That(fileEntry.Modifiers).Contains("sealed");
    }

    [Test]
    public async Task TryInspect_ValidAssembly_FileEntryHasExpectedProperties()
    {
        var inspector = new AssemblyInspector();
        await using var stream = File.OpenRead(CoreAssemblyPath);

        var result = inspector.TryInspect(stream)!;
        var fileEntry = result.PublicTypes.First(t => t.FullName == "DiffTool.Core.Models.FileEntry");
        var propertyNames = fileEntry.Properties.Select(p => p.Name).ToList();

        await Assert.That(propertyNames).Contains("RelativePath");
        await Assert.That(propertyNames).Contains("Hash");
        await Assert.That(propertyNames).Contains("SizeBytes");
        await Assert.That(propertyNames).Contains("LastModifiedUtc");
        await Assert.That(propertyNames).Contains("AssemblyInfo");
    }

    // -----------------------------------------------------------------------
    // 3. Known type — DiffKind enum
    // -----------------------------------------------------------------------

    [Test]
    public async Task TryInspect_ValidAssembly_FindsDiffKindEnum()
    {
        var inspector = new AssemblyInspector();
        await using var stream = File.OpenRead(CoreAssemblyPath);

        var result = inspector.TryInspect(stream)!;
        var diffKind = result.PublicTypes.FirstOrDefault(t => t.FullName == "DiffTool.Core.Models.DiffKind");

        await Assert.That(diffKind).IsNotNull();
        await Assert.That(diffKind!.Kind).IsEqualTo("enum");
    }

    // -----------------------------------------------------------------------
    // 4. Invalid / non-.NET streams
    // -----------------------------------------------------------------------

    [Test]
    public async Task TryInspect_RandomBytes_ReturnsNull()
    {
        var inspector = new AssemblyInspector();
        using var stream = new MemoryStream([0x00, 0x01, 0x02, 0x03]);

        var result = inspector.TryInspect(stream);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryInspect_EmptyStream_ReturnsNull()
    {
        var inspector = new AssemblyInspector();
        using var stream = new MemoryStream();

        var result = inspector.TryInspect(stream);

        await Assert.That(result).IsNull();
    }
}
