using DiffTool.Core.Models;
using DiffTool.Core.Scanning;
using DiffTool.Core.Tests.Fixtures;

namespace DiffTool.Core.Tests.Scanning;

/// <summary>
/// Verifies how <see cref="AssemblyInspector"/> reports each kind of public type, using the
/// fixtures in this test assembly as the subject.
/// </summary>
public class AssemblyInspectorShapeTests
{
    private const string Ns = "DiffTool.Core.Tests.Fixtures.";

    private static readonly Lazy<AssemblyInfo> Inspected = new(() =>
    {
        using var stream = File.OpenRead(typeof(FixtureBase).Assembly.Location);
        return new AssemblyInspector().TryInspect(stream)!;
    });

    private static AssemblyTypeInfo Type(string shortName)
        => Inspected.Value.PublicTypes.Single(t => t.FullName == Ns + shortName);

    [Test]
    public async Task Assembly_NameAndTargetFrameworkAreRead()
    {
        await Assert.That(Inspected.Value.Name).IsEqualTo("DiffTool.Core.Tests");
        await Assert.That(Inspected.Value.TargetFramework).IsNotNull();
        await Assert.That(Inspected.Value.TargetFramework!).Contains(".NETCoreApp");
    }

    [Test]
    public async Task Interface_ReportedAsInterfaceWithAbstractMembers()
    {
        var type = Type("IFixtureShape");

        await Assert.That(type.Kind).IsEqualTo("interface");
        await Assert.That(type.BaseType).IsNull();
        await Assert.That(type.Methods.Single(m => m.Name == "Run").Modifiers).Contains("abstract");
        await Assert.That(type.Properties.Single(p => p.Name == "Name").HasGetter).IsTrue();
        await Assert.That(type.Properties.Single(p => p.Name == "Name").HasSetter).IsFalse();
    }

    [Test]
    public async Task Enum_ReportedAsEnumWithConstantMembersAndNoValueField()
    {
        var type = Type("FixtureColor");

        await Assert.That(type.Kind).IsEqualTo("enum");
        await Assert.That(type.BaseType).IsNull();
        await Assert.That(type.Fields.Select(f => f.Name).ToList()).IsEquivalentTo(new[] { "Green", "Red" });
        await Assert.That(type.Fields.Single(f => f.Name == "Red").ConstValue).IsEqualTo("1");
        await Assert.That(type.Fields.All(f => f.IsConst && f.IsStatic)).IsTrue();
    }

    [Test]
    public async Task Delegate_ReportedAsDelegateWithoutBaseType()
    {
        var type = Type("FixtureHandler");

        await Assert.That(type.Kind).IsEqualTo("delegate");
        await Assert.That(type.BaseType).IsNull();
        await Assert.That(type.Modifiers).DoesNotContain("sealed");
        await Assert.That(type.Methods.Any(m => m.Name == "Invoke")).IsTrue();
    }

    [Test]
    public async Task Struct_ReportedAsStructWithPublicFields()
    {
        var type = Type("FixturePoint");

        await Assert.That(type.Kind).IsEqualTo("struct");
        await Assert.That(type.BaseType).IsNull();
        await Assert.That(type.Fields.Select(f => f.Name).ToList()).IsEquivalentTo(new[] { "X", "Y" });
    }

    [Test]
    public async Task StaticClass_HasStaticModifierOnly()
    {
        var type = Type("FixtureStatics");

        await Assert.That(type.Kind).IsEqualTo("class");
        await Assert.That(type.Modifiers.ToList()).IsEquivalentTo(new[] { "static" });
        await Assert.That(type.Methods.Single(m => m.Name == "Twice").Modifiers).Contains("static");
    }

    [Test]
    public async Task Constants_ReportConstAndValue_ReadonlyStaticsReportReadonly()
    {
        var type = Type("FixtureStatics");
        var answer = type.Fields.Single(f => f.Name == "Answer");
        var label = type.Fields.Single(f => f.Name == "Label");

        await Assert.That(answer.IsConst).IsTrue();
        await Assert.That(answer.IsStatic).IsTrue();
        await Assert.That(answer.ConstValue).IsEqualTo("42");
        await Assert.That(label.IsReadOnly).IsTrue();
        await Assert.That(label.IsStatic).IsTrue();
        await Assert.That(label.IsConst).IsFalse();
        await Assert.That(label.ConstValue).IsNull();
    }

    [Test]
    public async Task AbstractClass_HasAbstractModifierAndMemberModifiers()
    {
        var type = Type("FixtureBase");

        await Assert.That(type.Modifiers).Contains("abstract");
        await Assert.That(type.Methods.Single(m => m.Name == "Do").Modifiers).Contains("abstract");
        await Assert.That(type.Methods.Single(m => m.Name == "Maybe").Modifiers).Contains("virtual");
    }

    [Test]
    public async Task DerivedClass_ReportsBaseTypeInterfacesAndOverrides()
    {
        var type = Type("FixtureDerived");

        await Assert.That(type.BaseType).IsEqualTo(Ns + "FixtureBase");
        await Assert.That(type.Interfaces).Contains(Ns + "IFixtureShape");
        await Assert.That(type.Methods.Single(m => m.Name == "Do").Modifiers).Contains("override");
        await Assert.That(type.Methods.Single(m => m.Name == "Maybe").Modifiers).Contains("override");
    }

    [Test]
    public async Task SealedClass_HasSealedModifier()
    {
        await Assert.That(Type("FixtureSealed").Modifiers.ToList()).IsEquivalentTo(new[] { "sealed" });
    }

    [Test]
    public async Task Overloads_AreListedSeparatelyWithParameterTypes()
    {
        var overloads = Type("FixtureDerived").Methods.Where(m => m.Name == "Over").ToList();

        await Assert.That(overloads.Count).IsEqualTo(2);
        await Assert.That(overloads.Select(m => m.Parameters.Single()).ToList())
            .IsEquivalentTo(new[] { "System.Int32 value", "System.String value" });
    }

    [Test]
    public async Task NonPublicMembers_AreExcluded()
    {
        var type = Type("FixtureDerived");
        var methodNames = type.Methods.Select(m => m.Name).ToList();

        await Assert.That(methodNames).DoesNotContain("ProtectedMethod");
        await Assert.That(methodNames).DoesNotContain("InternalMethod");
        await Assert.That(methodNames).DoesNotContain("get_Name");
    }

    [Test]
    public async Task Properties_ReportAccessorVisibility()
    {
        var type = Type("FixtureDerived");
        var name = type.Properties.Single(p => p.Name == "Name");
        var readOnly = type.Properties.Single(p => p.Name == "ReadOnlyProp");
        var privateSetter = type.Properties.Single(p => p.Name == "PrivateSetter");

        await Assert.That(name.HasGetter && name.HasSetter).IsTrue();
        await Assert.That(readOnly.HasGetter).IsTrue();
        await Assert.That(readOnly.HasSetter).IsFalse();
        await Assert.That(privateSetter.HasGetter).IsTrue();
        await Assert.That(privateSetter.HasSetter).IsFalse();
    }

    [Test]
    public async Task StaticField_ReportsIsStatic()
    {
        var counter = Type("FixtureDerived").Fields.Single(f => f.Name == "Counter");

        await Assert.That(counter.IsStatic).IsTrue();
        await Assert.That(counter.IsConst).IsFalse();
    }

    [Test]
    public async Task GenericType_HasGenericModifierAndArityInName()
    {
        var type = Type("FixtureGeneric`1");

        await Assert.That(type.Modifiers).Contains("generic");
    }

    [Test]
    public async Task InternalTypes_AreExcluded()
    {
        var names = Inspected.Value.PublicTypes.Select(t => t.FullName).ToList();

        await Assert.That(names).DoesNotContain(Ns + "FixtureInternal");
    }

    [Test]
    public async Task PublicNestedType_IsIncludedWithSlashName()
    {
        var names = Inspected.Value.PublicTypes.Select(t => t.FullName).ToList();

        await Assert.That(names).Contains(Ns + "FixtureDerived/Inner");
    }

    [Test]
    public async Task NonPublicNestedTypes_AreExcluded()
    {
        var names = Inspected.Value.PublicTypes.Select(t => t.FullName).ToList();

        await Assert.That(names).DoesNotContain(Ns + "FixtureDerived/HiddenInner");
        await Assert.That(names).DoesNotContain(Ns + "FixtureInternal/NestedInInternal");
    }

    [Test]
    public async Task CompilerGeneratedTypes_AreExcluded()
    {
        var names = Inspected.Value.PublicTypes.Select(t => t.FullName).ToList();

        await Assert.That(names.Any(n => n.Contains('<'))).IsFalse();
    }

    [Test]
    public async Task Types_AreSortedOrdinally()
    {
        var names = Inspected.Value.PublicTypes.Select(t => t.FullName).ToList();

        await Assert.That(names.SequenceEqual(names.Order(StringComparer.Ordinal))).IsTrue();
    }
}
