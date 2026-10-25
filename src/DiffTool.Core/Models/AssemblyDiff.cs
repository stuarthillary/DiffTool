namespace DiffTool.Core.Models;

/// <summary>
/// Structural diff of the public API surface between two versions of a .NET assembly.
/// Produced by <see cref="DiffTool.Core.Diffing.AssemblyDiffer"/> and attached to a
/// <see cref="DiffEntry"/> when both snapshots carry <see cref="AssemblyInfo"/> for the same path.
/// </summary>
public sealed class AssemblyDiff
{
    /// <summary>True when the assembly version string changed.</summary>
    public bool VersionChanged { get; init; }

    /// <summary>True when the TargetFrameworkAttribute value changed.</summary>
    public bool TargetFrameworkChanged { get; init; }

    /// <summary>Types that appear only in the right (newer) assembly, sorted by full name.</summary>
    public required IReadOnlyList<AssemblyTypeInfo> AddedTypes { get; init; }

    /// <summary>Types that appear only in the left (older) assembly, sorted by full name.</summary>
    public required IReadOnlyList<AssemblyTypeInfo> RemovedTypes { get; init; }

    /// <summary>Types present in both assemblies whose public member sets differ.</summary>
    public required IReadOnlyList<AssemblyTypeDiff> ModifiedTypes { get; init; }

    /// <summary>True when any aspect of the assembly changed.</summary>
    public bool HasChanges =>
        VersionChanged || TargetFrameworkChanged ||
        AddedTypes.Count > 0 || RemovedTypes.Count > 0 || ModifiedTypes.Count > 0;
}

/// <summary>
/// Member-level diff for a single type whose public surface changed between two assembly versions.
/// Added and removed members are represented as full objects from the right and left assemblies
/// respectively. A change to a member's identity (parameter types, property or field type) appears
/// as one remove and one add, keyed on the full signature. Changes that keep the key but alter the
/// member (return type, modifiers, accessors, constant value) are described in <see cref="ModifiedMembers"/>.
/// </summary>
public sealed class AssemblyTypeDiff
{
    /// <summary>Fully-qualified type name.</summary>
    public required string FullName { get; init; }

    public required IReadOnlyList<AssemblyMethodInfo> AddedMethods { get; init; }
    public required IReadOnlyList<AssemblyMethodInfo> RemovedMethods { get; init; }
    public required IReadOnlyList<AssemblyPropertyInfo> AddedProperties { get; init; }
    public required IReadOnlyList<AssemblyPropertyInfo> RemovedProperties { get; init; }
    public required IReadOnlyList<AssemblyFieldInfo> AddedFields { get; init; }
    public required IReadOnlyList<AssemblyFieldInfo> RemovedFields { get; init; }

    /// <summary>Interfaces implemented only by the right (newer) type.</summary>
    public IReadOnlyList<string> AddedInterfaces { get; init; } = [];

    /// <summary>Interfaces implemented only by the left (older) type.</summary>
    public IReadOnlyList<string> RemovedInterfaces { get; init; } = [];

    /// <summary>
    /// Human-readable changes to the type itself (kind, base type, modifiers),
    /// e.g. "base type: A -> B".
    /// </summary>
    public IReadOnlyList<string> TypeChanges { get; init; } = [];

    /// <summary>
    /// Human-readable changes to members present in both versions under the same key,
    /// e.g. "Run(): return type System.Void -> System.String" or "Name: setter removed".
    /// </summary>
    public IReadOnlyList<string> ModifiedMembers { get; init; } = [];

    public bool HasChanges =>
        AddedMethods.Count > 0 || RemovedMethods.Count > 0 ||
        AddedProperties.Count > 0 || RemovedProperties.Count > 0 ||
        AddedFields.Count > 0 || RemovedFields.Count > 0 ||
        AddedInterfaces.Count > 0 || RemovedInterfaces.Count > 0 ||
        TypeChanges.Count > 0 || ModifiedMembers.Count > 0;
}
