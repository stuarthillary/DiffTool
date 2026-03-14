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
/// respectively. A signature change (e.g. return type, parameter types) appears as one remove
/// and one add, keyed on the full signature.
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

    public bool HasChanges =>
        AddedMethods.Count > 0 || RemovedMethods.Count > 0 ||
        AddedProperties.Count > 0 || RemovedProperties.Count > 0 ||
        AddedFields.Count > 0 || RemovedFields.Count > 0;
}
