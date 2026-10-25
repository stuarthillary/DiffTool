using DiffTool.Core.Diffing;
using DiffTool.Core.Models;

namespace DiffTool.Core.Tests.Diffing;

/// <summary>
/// Covers changes that keep a member's key but alter the member, plus type-level changes.
/// </summary>
public sealed class AssemblyDifferDetailTests
{
    private static readonly AssemblyDiffer Differ = new();

    private static AssemblyTypeInfo Type(
        string name = "N.T",
        string kind = "class",
        string? baseType = null,
        string[]? interfaces = null,
        string[]? modifiers = null,
        AssemblyMethodInfo[]? methods = null,
        AssemblyPropertyInfo[]? properties = null,
        AssemblyFieldInfo[]? fields = null)
        => new()
        {
            FullName = name,
            Kind = kind,
            BaseType = baseType,
            Interfaces = interfaces ?? [],
            Modifiers = modifiers ?? [],
            Methods = methods ?? [],
            Properties = properties ?? [],
            Fields = fields ?? [],
        };

    private static AssemblyInfo Assembly(params AssemblyTypeInfo[] types)
        => new() { Name = "A", Version = "1.0.0.0", PublicTypes = types };

    private static AssemblyMethodInfo Method(string returnType = "System.Void", string[]? modifiers = null)
        => new() { Name = "Run", ReturnType = returnType, Parameters = ["System.Int32 x"], Modifiers = modifiers ?? [] };

    private static AssemblyTypeDiff OnlyModified(AssemblyTypeInfo left, AssemblyTypeInfo right)
    {
        var diff = Differ.Compare(Assembly(left), Assembly(right));
        return diff.ModifiedTypes.Single();
    }

    [Test]
    public async Task MethodReturnTypeChanged_ReportedAsModifiedMember()
    {
        var result = OnlyModified(
            Type(methods: [Method("System.Void")]),
            Type(methods: [Method("System.String")]));

        await Assert.That(result.AddedMethods.Count).IsEqualTo(0);
        await Assert.That(result.RemovedMethods.Count).IsEqualTo(0);
        await Assert.That(result.ModifiedMembers.Count).IsEqualTo(1);
        await Assert.That(result.ModifiedMembers[0]).Contains("return type System.Void -> System.String");
    }

    [Test]
    public async Task MethodModifiersChanged_ReportedAsModifiedMember()
    {
        var result = OnlyModified(
            Type(methods: [Method(modifiers: ["virtual"])]),
            Type(methods: [Method(modifiers: [])]));

        await Assert.That(result.ModifiedMembers.Single()).Contains("modifiers virtual -> (none)");
    }

    [Test]
    public async Task MethodModifiersReordered_NotAChange()
    {
        var diff = Differ.Compare(
            Assembly(Type(methods: [Method(modifiers: ["static", "abstract"])])),
            Assembly(Type(methods: [Method(modifiers: ["abstract", "static"])])));

        await Assert.That(diff.HasChanges).IsFalse();
    }

    [Test]
    public async Task PropertySetterRemoved_ReportedAsModifiedMember()
    {
        var result = OnlyModified(
            Type(properties: [new() { Name = "P", Type = "System.String", HasGetter = true, HasSetter = true }]),
            Type(properties: [new() { Name = "P", Type = "System.String", HasGetter = true, HasSetter = false }]));

        await Assert.That(result.ModifiedMembers.Single()).IsEqualTo("P: setter removed");
    }

    [Test]
    public async Task PropertyGetterAdded_ReportedAsModifiedMember()
    {
        var result = OnlyModified(
            Type(properties: [new() { Name = "P", Type = "System.String", HasSetter = true }]),
            Type(properties: [new() { Name = "P", Type = "System.String", HasGetter = true, HasSetter = true }]));

        await Assert.That(result.ModifiedMembers.Single()).IsEqualTo("P: getter added");
    }

    [Test]
    public async Task FieldConstValueChanged_ReportedAsModifiedMember()
    {
        var result = OnlyModified(
            Type(fields: [new() { Name = "Max", Type = "System.Int32", IsConst = true, IsStatic = true, ConstValue = "1" }]),
            Type(fields: [new() { Name = "Max", Type = "System.Int32", IsConst = true, IsStatic = true, ConstValue = "2" }]));

        await Assert.That(result.ModifiedMembers.Single()).IsEqualTo("Max: value 1 -> 2");
    }

    [Test]
    public async Task FieldBecomesReadonlyAndStatic_BothReported()
    {
        var result = OnlyModified(
            Type(fields: [new() { Name = "F", Type = "System.Int32" }]),
            Type(fields: [new() { Name = "F", Type = "System.Int32", IsReadOnly = true, IsStatic = true }]));

        await Assert.That(result.ModifiedMembers.ToList())
            .IsEquivalentTo(new[] { "F: now static", "F: now readonly" });
    }

    [Test]
    public async Task BaseTypeChanged_ReportedOnType()
    {
        var result = OnlyModified(Type(baseType: "N.A"), Type(baseType: "N.B"));

        await Assert.That(result.TypeChanges.Single()).IsEqualTo("base type: N.A -> N.B");
    }

    [Test]
    public async Task BaseTypeRemoved_ReportsNone()
    {
        var result = OnlyModified(Type(baseType: "N.A"), Type());

        await Assert.That(result.TypeChanges.Single()).IsEqualTo("base type: N.A -> (none)");
    }

    [Test]
    public async Task KindChanged_ReportedOnType()
    {
        var result = OnlyModified(Type(kind: "class"), Type(kind: "struct"));

        await Assert.That(result.TypeChanges.Single()).IsEqualTo("kind: class -> struct");
    }

    [Test]
    public async Task TypeModifiersChanged_ReportedOnType()
    {
        var result = OnlyModified(Type(modifiers: ["sealed"]), Type(modifiers: []));

        await Assert.That(result.TypeChanges.Single()).IsEqualTo("modifiers: sealed -> (none)");
    }

    [Test]
    public async Task InterfacesAddedAndRemoved_Reported()
    {
        var result = OnlyModified(
            Type(interfaces: ["N.IOld", "N.IKept"]),
            Type(interfaces: ["N.IKept", "N.INew"]));

        await Assert.That(result.AddedInterfaces.ToList()).IsEquivalentTo(new[] { "N.INew" });
        await Assert.That(result.RemovedInterfaces.ToList()).IsEquivalentTo(new[] { "N.IOld" });
    }

    [Test]
    public async Task IdenticalDetails_NoChanges()
    {
        var type = Type(
            kind: "class", baseType: "N.A", interfaces: ["N.I"], modifiers: ["sealed"],
            methods: [Method("System.String", ["virtual"])],
            properties: [new() { Name = "P", Type = "System.String", HasGetter = true }],
            fields: [new() { Name = "F", Type = "System.Int32", IsConst = true, ConstValue = "1" }]);

        var diff = Differ.Compare(Assembly(type), Assembly(type));

        await Assert.That(diff.HasChanges).IsFalse();
    }

    [Test]
    public async Task UnsortedTypeLists_AreStillMatchedCorrectly()
    {
        // Culture-sensitive ordering puts "a_b" and "aB" differently from ordinal ordering.
        var left = Assembly(Type("N.aB"), Type("N.a_b"), Type("N.Z"));
        var right = Assembly(Type("N.Z"), Type("N.a_b"), Type("N.aB"), Type("N.New"));

        var diff = Differ.Compare(left, right);

        await Assert.That(diff.RemovedTypes.Count).IsEqualTo(0);
        await Assert.That(diff.ModifiedTypes.Count).IsEqualTo(0);
        await Assert.That(diff.AddedTypes.Select(t => t.FullName).ToList()).IsEquivalentTo(new[] { "N.New" });
    }

    [Test]
    public async Task NestedTypeAdded_ReportedAsAddedType()
    {
        var diff = Differ.Compare(
            Assembly(Type("N.Outer")),
            Assembly(Type("N.Outer"), Type("N.Outer/Inner")));

        await Assert.That(diff.AddedTypes.Single().FullName).IsEqualTo("N.Outer/Inner");
    }
}
