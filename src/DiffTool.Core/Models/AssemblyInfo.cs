namespace DiffTool.Core.Models;

/// <summary>
/// Metadata extracted from a .NET assembly (.dll or .exe).
/// </summary>
public sealed class AssemblyInfo
{
    /// <summary>Simple name of the assembly (e.g. "DiffTool.Core").</summary>
    public required string Name { get; init; }

    /// <summary>Four-part version string (e.g. "1.2.3.0").</summary>
    public required string Version { get; init; }

    /// <summary>Target framework moniker from TargetFrameworkAttribute, or null if absent.</summary>
    public string? TargetFramework { get; init; }

    /// <summary>All public non-compiler-generated types, sorted by full name.</summary>
    public required IReadOnlyList<AssemblyTypeInfo> PublicTypes { get; init; }
}

/// <summary>
/// Metadata for a single public type within an assembly.
/// </summary>
public sealed class AssemblyTypeInfo
{
    /// <summary>Fully-qualified type name using Mono.Cecil notation (e.g. "DiffTool.Core.Models.FileEntry").</summary>
    public required string FullName { get; init; }

    /// <summary>Type kind: "class", "interface", "struct", "enum", or "delegate".</summary>
    public required string Kind { get; init; }

    /// <summary>Full name of the base class, or null for interfaces/structs/enums/delegates/classes whose base is System.Object.</summary>
    public string? BaseType { get; init; }

    /// <summary>Full names of implemented interfaces, sorted.</summary>
    public required IReadOnlyList<string> Interfaces { get; init; }

    /// <summary>Type-level modifiers: any of "abstract", "sealed", "static", "generic".</summary>
    public required IReadOnlyList<string> Modifiers { get; init; }

    /// <summary>Public non-special methods (excludes property accessors), sorted by name.</summary>
    public required IReadOnlyList<AssemblyMethodInfo> Methods { get; init; }

    /// <summary>Public properties (at least one public accessor), sorted by name.</summary>
    public required IReadOnlyList<AssemblyPropertyInfo> Properties { get; init; }

    /// <summary>Public fields, sorted by name.</summary>
    public required IReadOnlyList<AssemblyFieldInfo> Fields { get; init; }
}

/// <summary>
/// Metadata for a public method.
/// </summary>
public sealed class AssemblyMethodInfo
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }

    /// <summary>Parameters formatted as "TypeFullName paramName".</summary>
    public required IReadOnlyList<string> Parameters { get; init; }

    /// <summary>Method modifiers: any of "static", "virtual", "override", "abstract".</summary>
    public required IReadOnlyList<string> Modifiers { get; init; }
}

/// <summary>
/// Metadata for a public property.
/// </summary>
public sealed class AssemblyPropertyInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
}

/// <summary>
/// Metadata for a public field.
/// </summary>
public sealed class AssemblyFieldInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsStatic { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsConst { get; init; }

    /// <summary>String representation of the constant value, or null for non-const fields.</summary>
    public string? ConstValue { get; init; }
}
