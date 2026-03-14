using DiffTool.Core.Diffing;
using DiffTool.Core.Models;

namespace DiffTool.Core.Tests.Diffing;

public sealed class AssemblyDifferTests
{
    private static readonly AssemblyDiffer Differ = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AssemblyInfo MakeAssembly(
        string version = "1.0.0.0",
        string? targetFramework = "net10.0",
        params AssemblyTypeInfo[] types)
        => new()
        {
            Name            = "TestAssembly",
            Version         = version,
            TargetFramework = targetFramework,
            PublicTypes     = types.OrderBy(t => t.FullName).ToList().AsReadOnly(),
        };

    private static AssemblyTypeInfo MakeType(
        string fullName,
        IEnumerable<AssemblyMethodInfo>?   methods    = null,
        IEnumerable<AssemblyPropertyInfo>? properties = null,
        IEnumerable<AssemblyFieldInfo>?    fields     = null)
        => new()
        {
            FullName   = fullName,
            Kind       = "class",
            Interfaces = [],
            Modifiers  = [],
            Methods    = (methods    ?? []).OrderBy(m => m.Name).ToList().AsReadOnly(),
            Properties = (properties ?? []).OrderBy(p => p.Name).ToList().AsReadOnly(),
            Fields     = (fields     ?? []).OrderBy(f => f.Name).ToList().AsReadOnly(),
        };

    private static AssemblyMethodInfo MakeMethod(string name, string returnType = "System.Void", params string[] paramTypes)
        => new()
        {
            Name       = name,
            ReturnType = returnType,
            Parameters = paramTypes.Select((t, i) => $"{t} arg{i}").ToList().AsReadOnly(),
            Modifiers  = [],
        };

    private static AssemblyPropertyInfo MakeProp(string name, string type = "System.String")
        => new() { Name = name, Type = type, HasGetter = true };

    private static AssemblyFieldInfo MakeField(string name, string type = "System.Int32")
        => new() { Name = name, Type = type };

    // ── 1. Identical assemblies ───────────────────────────────────────────────

    [Test]
    public async Task Compare_IdenticalAssemblies_NoChanges()
    {
        var type = MakeType("Foo.Bar", [MakeMethod("Run")], [MakeProp("Name")]);
        var left  = MakeAssembly(types: type);
        var right = MakeAssembly(types: type);

        var result = Differ.Compare(left, right);

        await Assert.That(result.HasChanges).IsFalse();
        await Assert.That(result.AddedTypes.Count).IsEqualTo(0);
        await Assert.That(result.RemovedTypes.Count).IsEqualTo(0);
        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(0);
    }

    // ── 2. Empty assemblies ───────────────────────────────────────────────────

    [Test]
    public async Task Compare_BothEmpty_NoChanges()
    {
        var result = Differ.Compare(MakeAssembly(), MakeAssembly());

        await Assert.That(result.HasChanges).IsFalse();
    }

    // ── 3. Type added ─────────────────────────────────────────────────────────

    [Test]
    public async Task Compare_TypeAddedInRight_ReportedAsAdded()
    {
        var left  = MakeAssembly();
        var right = MakeAssembly(types: MakeType("Foo.NewService"));

        var result = Differ.Compare(left, right);

        await Assert.That(result.AddedTypes.Count).IsEqualTo(1);
        await Assert.That(result.AddedTypes[0].FullName).IsEqualTo("Foo.NewService");
        await Assert.That(result.RemovedTypes.Count).IsEqualTo(0);
        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(0);
        await Assert.That(result.HasChanges).IsTrue();
    }

    // ── 4. Type removed ───────────────────────────────────────────────────────

    [Test]
    public async Task Compare_TypeRemovedFromRight_ReportedAsRemoved()
    {
        var left  = MakeAssembly(types: MakeType("Foo.OldHelper"));
        var right = MakeAssembly();

        var result = Differ.Compare(left, right);

        await Assert.That(result.RemovedTypes.Count).IsEqualTo(1);
        await Assert.That(result.RemovedTypes[0].FullName).IsEqualTo("Foo.OldHelper");
        await Assert.That(result.AddedTypes.Count).IsEqualTo(0);
        await Assert.That(result.HasChanges).IsTrue();
    }

    // ── 5. Version changed ────────────────────────────────────────────────────

    [Test]
    public async Task Compare_VersionChanged_VersionChangedIsTrue()
    {
        var left  = MakeAssembly(version: "1.0.0.0");
        var right = MakeAssembly(version: "2.0.0.0");

        var result = Differ.Compare(left, right);

        await Assert.That(result.VersionChanged).IsTrue();
        await Assert.That(result.HasChanges).IsTrue();
    }

    [Test]
    public async Task Compare_SameVersion_VersionChangedIsFalse()
    {
        var result = Differ.Compare(MakeAssembly("1.0.0.0"), MakeAssembly("1.0.0.0"));

        await Assert.That(result.VersionChanged).IsFalse();
    }

    // ── 6. TargetFramework changed ────────────────────────────────────────────

    [Test]
    public async Task Compare_TargetFrameworkChanged_TargetFrameworkChangedIsTrue()
    {
        var left  = MakeAssembly(targetFramework: "net8.0");
        var right = MakeAssembly(targetFramework: "net10.0");

        var result = Differ.Compare(left, right);

        await Assert.That(result.TargetFrameworkChanged).IsTrue();
        await Assert.That(result.HasChanges).IsTrue();
    }

    // ── 7. Method added to existing type ─────────────────────────────────────

    [Test]
    public async Task Compare_MethodAddedToType_ReportedInModifiedTypes()
    {
        var left  = MakeAssembly(types: MakeType("Foo.Scanner", [MakeMethod("ScanAsync", "System.Threading.Tasks.Task", "System.String")]));
        var right = MakeAssembly(types: MakeType("Foo.Scanner",
            [MakeMethod("ScanAsync",     "System.Threading.Tasks.Task", "System.String"),
             MakeMethod("ScanDeepAsync", "System.Threading.Tasks.Task", "System.String", "System.Boolean")]));

        var result = Differ.Compare(left, right);

        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(1);
        var td = result.ModifiedTypes[0];
        await Assert.That(td.FullName).IsEqualTo("Foo.Scanner");
        await Assert.That(td.AddedMethods.Count).IsEqualTo(1);
        await Assert.That(td.AddedMethods[0].Name).IsEqualTo("ScanDeepAsync");
        await Assert.That(td.RemovedMethods.Count).IsEqualTo(0);
    }

    // ── 8. Method removed from existing type ─────────────────────────────────

    [Test]
    public async Task Compare_MethodRemovedFromType_ReportedInModifiedTypes()
    {
        var left  = MakeAssembly(types: MakeType("Foo.Scanner",
            [MakeMethod("ScanAsync",     "System.Threading.Tasks.Task", "System.String"),
             MakeMethod("ScanDeepAsync", "System.Threading.Tasks.Task", "System.String", "System.Boolean")]));
        var right = MakeAssembly(types: MakeType("Foo.Scanner", [MakeMethod("ScanAsync", "System.Threading.Tasks.Task", "System.String")]));

        var result = Differ.Compare(left, right);

        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(1);
        var td = result.ModifiedTypes[0];
        await Assert.That(td.RemovedMethods.Count).IsEqualTo(1);
        await Assert.That(td.RemovedMethods[0].Name).IsEqualTo("ScanDeepAsync");
        await Assert.That(td.AddedMethods.Count).IsEqualTo(0);
    }

    // ── 9. Overloads are keyed independently ──────────────────────────────────

    [Test]
    public async Task Compare_Overloads_KeyedByFullSignature_TreatedIndependently()
    {
        // Both overloads exist in left; only the string overload exists in right.
        var left = MakeAssembly(types: MakeType("Foo.Logger",
            [MakeMethod("Log", "System.Void", "System.String"),
             MakeMethod("Log", "System.Void", "System.Int32")]));
        var right = MakeAssembly(types: MakeType("Foo.Logger",
            [MakeMethod("Log", "System.Void", "System.String")]));

        var result = Differ.Compare(left, right);

        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(1);
        var td = result.ModifiedTypes[0];
        await Assert.That(td.RemovedMethods.Count).IsEqualTo(1);
        // The removed overload has System.Int32 parameter
        await Assert.That(td.RemovedMethods[0].Parameters[0]).IsEqualTo("System.Int32 arg0");
        await Assert.That(td.AddedMethods.Count).IsEqualTo(0);
    }

    // ── 10. Signature change appears as remove + add ──────────────────────────

    [Test]
    public async Task Compare_MethodSignatureChange_AppearsAsRemoveAndAdd()
    {
        // Parameter type changes from System.String to System.Uri.
        var left  = MakeAssembly(types: MakeType("Foo.Client", [MakeMethod("Connect", "System.Void", "System.String")]));
        var right = MakeAssembly(types: MakeType("Foo.Client", [MakeMethod("Connect", "System.Void", "System.Uri")]));

        var result = Differ.Compare(left, right);

        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(1);
        var td = result.ModifiedTypes[0];
        await Assert.That(td.RemovedMethods.Count).IsEqualTo(1);
        await Assert.That(td.AddedMethods.Count).IsEqualTo(1);
        await Assert.That(td.RemovedMethods[0].Parameters[0]).IsEqualTo("System.String arg0");
        await Assert.That(td.AddedMethods[0].Parameters[0]).IsEqualTo("System.Uri arg0");
    }

    // ── 11. Property added ────────────────────────────────────────────────────

    [Test]
    public async Task Compare_PropertyAdded_ReportedInModifiedTypes()
    {
        var left  = MakeAssembly(types: MakeType("Foo.Options"));
        var right = MakeAssembly(types: MakeType("Foo.Options", properties: [MakeProp("Timeout", "System.TimeSpan")]));

        var result = Differ.Compare(left, right);

        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(1);
        var td = result.ModifiedTypes[0];
        await Assert.That(td.AddedProperties.Count).IsEqualTo(1);
        await Assert.That(td.AddedProperties[0].Name).IsEqualTo("Timeout");
        await Assert.That(td.RemovedProperties.Count).IsEqualTo(0);
    }

    // ── 12. Property type change appears as remove + add ─────────────────────

    [Test]
    public async Task Compare_PropertyTypeChange_AppearsAsRemoveAndAdd()
    {
        var left  = MakeAssembly(types: MakeType("Foo.Options", properties: [MakeProp("Timeout", "System.Int32")]));
        var right = MakeAssembly(types: MakeType("Foo.Options", properties: [MakeProp("Timeout", "System.TimeSpan")]));

        var result = Differ.Compare(left, right);

        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(1);
        var td = result.ModifiedTypes[0];
        await Assert.That(td.RemovedProperties.Count).IsEqualTo(1);
        await Assert.That(td.AddedProperties.Count).IsEqualTo(1);
        await Assert.That(td.RemovedProperties[0].Type).IsEqualTo("System.Int32");
        await Assert.That(td.AddedProperties[0].Type).IsEqualTo("System.TimeSpan");
    }

    // ── 13. Field added ───────────────────────────────────────────────────────

    [Test]
    public async Task Compare_FieldAdded_ReportedInModifiedTypes()
    {
        var left  = MakeAssembly(types: MakeType("Foo.Constants"));
        var right = MakeAssembly(types: MakeType("Foo.Constants", fields: [MakeField("MaxRetries", "System.Int32")]));

        var result = Differ.Compare(left, right);

        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(1);
        var td = result.ModifiedTypes[0];
        await Assert.That(td.AddedFields.Count).IsEqualTo(1);
        await Assert.That(td.AddedFields[0].Name).IsEqualTo("MaxRetries");
        await Assert.That(td.RemovedFields.Count).IsEqualTo(0);
    }

    // ── 14. Multiple types — some added, some removed, some modified ──────────

    [Test]
    public async Task Compare_MixedChanges_AllCategoriesPopulated()
    {
        var left = MakeAssembly(types:
        [
            MakeType("Foo.OldService"),
            MakeType("Foo.Scanner", [MakeMethod("ScanAsync", "System.Threading.Tasks.Task")]),
            MakeType("Foo.Unchanged", [MakeMethod("Run")]),
        ]);
        var right = MakeAssembly(types:
        [
            MakeType("Foo.NewService"),
            MakeType("Foo.Scanner",
                [MakeMethod("ScanAsync",     "System.Threading.Tasks.Task"),
                 MakeMethod("ScanDeepAsync", "System.Threading.Tasks.Task")]),
            MakeType("Foo.Unchanged", [MakeMethod("Run")]),
        ]);

        var result = Differ.Compare(left, right);

        await Assert.That(result.AddedTypes.Count).IsEqualTo(1);
        await Assert.That(result.AddedTypes[0].FullName).IsEqualTo("Foo.NewService");
        await Assert.That(result.RemovedTypes.Count).IsEqualTo(1);
        await Assert.That(result.RemovedTypes[0].FullName).IsEqualTo("Foo.OldService");
        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(1);
        await Assert.That(result.ModifiedTypes[0].FullName).IsEqualTo("Foo.Scanner");
        await Assert.That(result.HasChanges).IsTrue();
    }

    // ── 15. Unchanged type not included in ModifiedTypes ─────────────────────

    [Test]
    public async Task Compare_TypeUnchanged_NotIncludedInModifiedTypes()
    {
        var type  = MakeType("Foo.Stable", [MakeMethod("Execute")]);
        var left  = MakeAssembly(types: type);
        var right = MakeAssembly(types: type);

        var result = Differ.Compare(left, right);

        await Assert.That(result.ModifiedTypes.Count).IsEqualTo(0);
    }
}
