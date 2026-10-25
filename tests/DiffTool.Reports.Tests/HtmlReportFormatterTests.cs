using System.Text;
using DiffTool.Core.Models;
using DiffTool.Reports;

namespace DiffTool.Core.Tests.Reports;

public sealed class HtmlReportFormatterTests
{
    private static readonly DateTimeOffset T1 = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static SnapshotDiff MakeDiff(
        string leftPath = "C:/left",
        string rightPath = "C:/right",
        params DiffEntry[] entries)
    {
        var added     = entries.Count(e => e.Kind == DiffKind.Added);
        var removed   = entries.Count(e => e.Kind == DiffKind.Removed);
        var modified  = entries.Count(e => e.Kind == DiffKind.Modified);
        var unchanged = entries.Count(e => e.Kind == DiffKind.Unchanged);

        return new SnapshotDiff
        {
            LeftPath           = leftPath,
            RightPath          = rightPath,
            LeftCapturedAtUtc  = T1,
            RightCapturedAtUtc = T2,
            Entries            = entries.ToList(),
            Summary            = new DiffSummary
            {
                Added     = added,
                Removed   = removed,
                Modified  = modified,
                Unchanged = unchanged
            }
        };
    }

    private static DiffEntry AddedEntry(string path, long sizeBytes = 100) => new()
    {
        RelativePath = path,
        Kind         = DiffKind.Added,
        Right        = new FileEntry { RelativePath = path, SizeBytes = sizeBytes, LastModifiedUtc = T1, Hash = "abc" }
    };

    private static DiffEntry RemovedEntry(string path, long sizeBytes = 100) => new()
    {
        RelativePath = path,
        Kind         = DiffKind.Removed,
        Left         = new FileEntry { RelativePath = path, SizeBytes = sizeBytes, LastModifiedUtc = T1, Hash = "abc" }
    };

    private static DiffEntry ModifiedEntry(string path, IReadOnlyList<string>? changedProps = null) => new()
    {
        RelativePath       = path,
        Kind               = DiffKind.Modified,
        Left               = new FileEntry { RelativePath = path, SizeBytes = 100, LastModifiedUtc = T1, Hash = "old" },
        Right              = new FileEntry { RelativePath = path, SizeBytes = 200, LastModifiedUtc = T2, Hash = "new" },
        ChangedProperties  = changedProps ?? ["Hash"]
    };

    private static DiffEntry UnchangedEntry(string path) => new()
    {
        RelativePath = path,
        Kind         = DiffKind.Unchanged,
        Left         = new FileEntry { RelativePath = path, SizeBytes = 100, LastModifiedUtc = T1, Hash = "abc" },
        Right        = new FileEntry { RelativePath = path, SizeBytes = 100, LastModifiedUtc = T1, Hash = "abc" }
    };

    private static async Task<string> RenderAsync(SnapshotDiff diff, CancellationToken ct = default)
    {
        var formatter = new HtmlReportFormatter();
        using var ms = new MemoryStream();
        await formatter.RenderAsync(diff, ms, ct);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // ── 1. Empty diff ────────────────────────────────────────────────────────

    [Test]
    public async Task EmptyDiff_OutputIsValidHtmlWithZeroSummaryCounts()
    {
        var diff = MakeDiff();
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("<!DOCTYPE html>");
        await Assert.That(html).Contains("<title>Directory Diff Report</title>");
        // Summary cards must all show 0
        await Assert.That(html).Contains(">0<");
    }

    // ── 2. All-added ─────────────────────────────────────────────────────────

    [Test]
    public async Task AllAddedDiff_HtmlContainsAddedBadge()
    {
        var diff = MakeDiff(entries:
        [
            AddedEntry("file1.txt"),
            AddedEntry("file2.txt")
        ]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("badge-added");
        await Assert.That(html).Contains("+ Added");
    }

    // ── 3. All-removed ───────────────────────────────────────────────────────

    [Test]
    public async Task AllRemovedDiff_HtmlContainsRemovedBadge()
    {
        var diff = MakeDiff(entries:
        [
            RemovedEntry("file1.txt"),
            RemovedEntry("file2.txt")
        ]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("badge-removed");
        await Assert.That(html).Contains("- Removed");
    }

    // ── 4. Mixed diff ────────────────────────────────────────────────────────

    [Test]
    public async Task MixedDiff_AllFourStatusTypesAppearInOutput()
    {
        var diff = MakeDiff(entries:
        [
            AddedEntry("added.txt"),
            RemovedEntry("removed.txt"),
            ModifiedEntry("modified.txt"),
            UnchangedEntry("unchanged.txt")
        ]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("badge-added");
        await Assert.That(html).Contains("badge-removed");
        await Assert.That(html).Contains("badge-modified");
        await Assert.That(html).Contains("badge-unchanged");
        await Assert.That(html).Contains("+ Added");
        await Assert.That(html).Contains("- Removed");
        await Assert.That(html).Contains("~ Modified");
        await Assert.That(html).Contains("= Unchanged");
    }

    // ── 5. ModifiedEntry with ChangedProperties ───────────────────────────────

    [Test]
    public async Task ModifiedEntryWithChangedProperties_PropertyNamesAppearInOutput()
    {
        var diff = MakeDiff(entries:
        [
            ModifiedEntry("file.txt", ["Hash", "Size"])
        ]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("Hash");
        await Assert.That(html).Contains("Size");
    }

    // ── 6. Summary counts ────────────────────────────────────────────────────

    [Test]
    public async Task SummaryCounts_CorrectNumericValuesAppearInOutput()
    {
        var diff = MakeDiff(entries:
        [
            AddedEntry("a.txt"),
            AddedEntry("b.txt"),
            RemovedEntry("c.txt"),
            ModifiedEntry("d.txt"),
            UnchangedEntry("e.txt"),
            UnchangedEntry("f.txt"),
            UnchangedEntry("g.txt")
        ]);
        var html = await RenderAsync(diff);

        // Summary section counts: 2 added, 1 removed, 1 modified, 3 unchanged
        await Assert.That(diff.Summary.Added).IsEqualTo(2);
        await Assert.That(diff.Summary.Removed).IsEqualTo(1);
        await Assert.That(diff.Summary.Modified).IsEqualTo(1);
        await Assert.That(diff.Summary.Unchanged).IsEqualTo(3);
        await Assert.That(diff.Summary.Total).IsEqualTo(7);

        await Assert.That(html).Contains(">2<");
        await Assert.That(html).Contains(">1<");
        await Assert.That(html).Contains(">3<");
        await Assert.That(html).Contains(">7<");
    }

    // ── 7. Self-contained HTML ───────────────────────────────────────────────

    [Test]
    public async Task Output_DoesNotContainExternalStylesheetOrCdnReference()
    {
        var diff = MakeDiff();
        var html = await RenderAsync(diff);

        await Assert.That(html.Contains("<link")).IsFalse();
        await Assert.That(html.Contains("src=\"http")).IsFalse();
    }

    // ── 8. Root path appears ─────────────────────────────────────────────────

    [Test]
    public async Task LeftSnapshotRootPath_AppearsInOutput()
    {
        var diff = MakeDiff(leftPath: "D:/projects/myapp");
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("D:/projects/myapp");
    }

    // ── 9. File paths appear ─────────────────────────────────────────────────

    [Test]
    public async Task EntryRelativePaths_AppearInOutput()
    {
        var diff = MakeDiff(entries:
        [
            AddedEntry("src/controllers/HomeController.cs"),
            RemovedEntry("src/views/old-layout.html")
        ]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("src/controllers/HomeController.cs");
        await Assert.That(html).Contains("src/views/old-layout.html");
    }

    // ── 10. Output is non-empty ──────────────────────────────────────────────

    [Test]
    public async Task RenderAsync_WritesNonEmptyOutput()
    {
        var diff = MakeDiff();
        var html = await RenderAsync(diff);

        await Assert.That(html.Length).IsGreaterThan(0);
    }

    // ── 11. Cancellation respected ───────────────────────────────────────────

    [Test]
    public async Task RenderAsync_WithPreCancelledToken_ThrowsOperationCanceledException()
    {
        var diff = MakeDiff(entries:
        [
            AddedEntry("file1.txt"),
            AddedEntry("file2.txt")
        ]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.That(async () =>
        {
            using var ms = new MemoryStream();
            var formatter = new HtmlReportFormatter();
            await formatter.RenderAsync(diff, ms, cts.Token);
        }).Throws<OperationCanceledException>();
    }

    // ── Extra: Report title ──────────────────────────────────────────────────

    [Test]
    public async Task Output_ContainsExpectedReportTitle()
    {
        var diff = MakeDiff();
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("<title>Directory Diff Report</title>");
        await Assert.That(html).Contains("<h1>Directory Diff Report</h1>");
    }

    // ── Extra: HTML escaping ─────────────────────────────────────────────────

    [Test]
    public async Task RelativePathWithSpecialChars_IsHtmlEscapedInOutput()
    {
        var diff = MakeDiff(entries:
        [
            AddedEntry("folder<test>/file&name.txt")
        ]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("folder&lt;test&gt;/file&amp;name.txt");
        await Assert.That(html.Contains("folder<test>")).IsFalse();
    }

    // ── Assembly detail rows ─────────────────────────────────────────────────

    private static AssemblyInfo MakeAssemblyInfo(string name, string version, params AssemblyTypeInfo[] types)
        => new()
        {
            Name        = name,
            Version     = version,
            PublicTypes = types.OrderBy(t => t.FullName).ToList().AsReadOnly(),
        };

    private static AssemblyTypeInfo MakeTypeInfo(string fullName, string kind = "class")
        => new()
        {
            FullName   = fullName,
            Kind       = kind,
            Interfaces = [],
            Modifiers  = [],
            Methods    = [],
            Properties = [],
            Fields     = [],
        };

    private static AssemblyMethodInfo MakeMethodInfo(string name, string returnType = "System.Void", params string[] paramTypes)
        => new()
        {
            Name       = name,
            ReturnType = returnType,
            Parameters = paramTypes.Select((t, i) => $"{t} arg{i}").ToList().AsReadOnly(),
            Modifiers  = [],
        };

    [Test]
    public async Task AddedEntryWithAssemblyInfo_RendersDetailRow()
    {
        var info = MakeAssemblyInfo("MyLib", "1.0.0.0", MakeTypeInfo("MyLib.ServiceA"), MakeTypeInfo("MyLib.ServiceB"));
        var entry = new DiffEntry
        {
            RelativePath = "MyLib.dll",
            Kind         = DiffKind.Added,
            Right        = new FileEntry { RelativePath = "MyLib.dll", SizeBytes = 1024, LastModifiedUtc = T1, Hash = "abc", AssemblyInfo = info },
        };
        var diff = MakeDiff(entries: [entry]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("<tr class=\"asm-detail\">");
        await Assert.That(html).Contains("New .NET assembly");
        await Assert.That(html).Contains("MyLib");
        await Assert.That(html).Contains("MyLib.ServiceA");
        await Assert.That(html).Contains("MyLib.ServiceB");
        await Assert.That(html).Contains("asm-added-item");
    }

    [Test]
    public async Task RemovedEntryWithAssemblyInfo_RendersDetailRow()
    {
        var info = MakeAssemblyInfo("Legacy", "0.9.0.0", MakeTypeInfo("Legacy.OldClass"));
        var entry = new DiffEntry
        {
            RelativePath = "Legacy.dll",
            Kind         = DiffKind.Removed,
            Left         = new FileEntry { RelativePath = "Legacy.dll", SizeBytes = 512, LastModifiedUtc = T1, Hash = "xyz", AssemblyInfo = info },
        };
        var diff = MakeDiff(entries: [entry]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("<tr class=\"asm-detail\">");
        await Assert.That(html).Contains("Removed .NET assembly");
        await Assert.That(html).Contains("Legacy.OldClass");
        await Assert.That(html).Contains("asm-removed-item");
    }

    [Test]
    public async Task ModifiedEntryWithAssemblyDiff_RendersDetailRowWithTypeChanges()
    {
        var addedType   = MakeTypeInfo("Foo.NewService");
        var removedType = MakeTypeInfo("Foo.OldHelper");
        var assemblyDiff = new AssemblyDiff
        {
            VersionChanged         = true,
            TargetFrameworkChanged = false,
            AddedTypes             = [addedType],
            RemovedTypes           = [removedType],
            ModifiedTypes          = [],
        };
        var leftInfo  = MakeAssemblyInfo("Foo", "1.0.0.0");
        var rightInfo = MakeAssemblyInfo("Foo", "2.0.0.0");
        var entry = new DiffEntry
        {
            RelativePath      = "Foo.dll",
            Kind              = DiffKind.Modified,
            Left              = new FileEntry { RelativePath = "Foo.dll", SizeBytes = 100, LastModifiedUtc = T1, Hash = "old", AssemblyInfo = leftInfo },
            Right             = new FileEntry { RelativePath = "Foo.dll", SizeBytes = 200, LastModifiedUtc = T2, Hash = "new", AssemblyInfo = rightInfo },
            ChangedProperties = ["Hash", "AssemblyInfo"],
            AssemblyDiff      = assemblyDiff,
        };
        var diff = MakeDiff(entries: [entry]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("<tr class=\"asm-detail\">");
        await Assert.That(html).Contains("Assembly changes");
        await Assert.That(html).Contains("Foo.NewService");
        await Assert.That(html).Contains("Foo.OldHelper");
        await Assert.That(html).Contains("Added types");
        await Assert.That(html).Contains("Removed types");
    }

    [Test]
    public async Task ModifiedEntryWithAssemblyDiff_ChangesColumnShowsTypeSummary()
    {
        var assemblyDiff = new AssemblyDiff
        {
            VersionChanged         = false,
            TargetFrameworkChanged = false,
            AddedTypes             = [MakeTypeInfo("Foo.A"), MakeTypeInfo("Foo.B")],
            RemovedTypes           = [MakeTypeInfo("Foo.C")],
            ModifiedTypes          = [],
        };
        var entry = new DiffEntry
        {
            RelativePath      = "Foo.dll",
            Kind              = DiffKind.Modified,
            Left              = new FileEntry { RelativePath = "Foo.dll", SizeBytes = 100, LastModifiedUtc = T1, Hash = "old" },
            Right             = new FileEntry { RelativePath = "Foo.dll", SizeBytes = 200, LastModifiedUtc = T2, Hash = "new" },
            ChangedProperties = ["Hash", "AssemblyInfo"],
            AssemblyDiff      = assemblyDiff,
        };
        var diff = MakeDiff(entries: [entry]);
        var html = await RenderAsync(diff);

        // Summary should show counts, not raw property names
        await Assert.That(html).Contains("+2 types");
        await Assert.That(html).Contains("-1 type");
    }

    [Test]
    public async Task ModifiedEntryWithAssemblyDiff_MethodChangesRendered()
    {
        var typeDiff = new AssemblyTypeDiff
        {
            FullName          = "Foo.Scanner",
            AddedMethods      = [MakeMethodInfo("ScanDeepAsync", "System.Threading.Tasks.Task", "System.String", "System.Boolean")],
            RemovedMethods    = [],
            AddedProperties   = [],
            RemovedProperties = [],
            AddedFields       = [],
            RemovedFields     = [],
        };
        var assemblyDiff = new AssemblyDiff
        {
            VersionChanged         = false,
            TargetFrameworkChanged = false,
            AddedTypes             = [],
            RemovedTypes           = [],
            ModifiedTypes          = [typeDiff],
        };
        var entry = new DiffEntry
        {
            RelativePath      = "Foo.dll",
            Kind              = DiffKind.Modified,
            Left              = new FileEntry { RelativePath = "Foo.dll", SizeBytes = 100, LastModifiedUtc = T1, Hash = "old" },
            Right             = new FileEntry { RelativePath = "Foo.dll", SizeBytes = 200, LastModifiedUtc = T2, Hash = "new" },
            ChangedProperties = ["Hash", "AssemblyInfo"],
            AssemblyDiff      = assemblyDiff,
        };
        var diff = MakeDiff(entries: [entry]);
        var html = await RenderAsync(diff);

        await Assert.That(html).Contains("Foo.Scanner");
        await Assert.That(html).Contains("ScanDeepAsync");
        await Assert.That(html).Contains("asm-added-item");
    }

    [Test]
    public async Task ModifiedEntryWithAssemblyDiff_TypeAndMemberDetailChangesRenderedAndEscaped()
    {
        var typeDiff = new AssemblyTypeDiff
        {
            FullName          = "Foo.Widget",
            AddedMethods      = [],
            RemovedMethods    = [],
            AddedProperties   = [],
            RemovedProperties = [],
            AddedFields       = [],
            RemovedFields     = [],
            AddedInterfaces   = ["Foo.INew"],
            RemovedInterfaces = ["Foo.IOld"],
            TypeChanges       = ["base type: Foo.A -> Foo.B"],
            ModifiedMembers   = ["Run(): return type System.Void -> List<System.String>"],
        };
        var entry = new DiffEntry
        {
            RelativePath      = "Foo.dll",
            Kind              = DiffKind.Modified,
            Left              = new FileEntry { RelativePath = "Foo.dll", SizeBytes = 100, LastModifiedUtc = T1, Hash = "old" },
            Right             = new FileEntry { RelativePath = "Foo.dll", SizeBytes = 200, LastModifiedUtc = T2, Hash = "new" },
            ChangedProperties = ["Hash", "AssemblyInfo"],
            AssemblyDiff      = new AssemblyDiff { AddedTypes = [], RemovedTypes = [], ModifiedTypes = [typeDiff] },
        };

        var html = await RenderAsync(MakeDiff(entries: [entry]));

        await Assert.That(html).Contains("base type: Foo.A -&gt; Foo.B");
        await Assert.That(html).Contains("+ implements Foo.INew");
        await Assert.That(html).Contains("- implements Foo.IOld");
        await Assert.That(html).Contains("List&lt;System.String&gt;");
        await Assert.That(html).DoesNotContain("List<System.String>");
    }

    [Test]
    public async Task ModifiedEntryWithoutAssemblyDiff_DefaultChangedPropertiesShown()
    {
        // When no AssemblyDiff is set (no inspector was run), falls back to ChangedProperties text.
        var entry = ModifiedEntry("bin/app.dll", ["Hash", "SizeBytes"]);
        var diff  = MakeDiff(entries: [entry]);
        var html  = await RenderAsync(diff);

        await Assert.That(html).Contains("Hash");
        await Assert.That(html).Contains("SizeBytes");
        await Assert.That(html.Contains("<tr class=\"asm-detail\">")).IsFalse();
    }

    [Test]
    public async Task AddedEntryWithoutAssemblyInfo_NoDetailRow()
    {
        var diff = MakeDiff(entries: [AddedEntry("plain.txt")]);
        var html = await RenderAsync(diff);

        await Assert.That(html.Contains("<tr class=\"asm-detail\">")).IsFalse();
    }
}
