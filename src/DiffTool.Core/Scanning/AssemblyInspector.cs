using System.Diagnostics;
using DiffTool.Core.Models;
using Mono.Cecil;

namespace DiffTool.Core.Scanning;

/// <summary>
/// Reads .NET assembly metadata using Mono.Cecil.
/// The assembly is never loaded into the CLR — Cecil reads raw PE metadata from the stream.
/// </summary>
public sealed class AssemblyInspector : IAssemblyInspector
{
    private static readonly ReaderParameters s_readerParams = new() { ReadSymbols = false };

    /// <inheritdoc />
    public AssemblyInfo? TryInspect(Stream stream)
    {
        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(stream, s_readerParams);

            var targetFramework = assembly.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.FullName ==
                    "System.Runtime.Versioning.TargetFrameworkAttribute")
                ?.ConstructorArguments is [{ Value: { } tfv }, ..]
                    ? tfv.ToString()
                    : null;

            var publicTypes = EnumeratePublicTypes(assembly.MainModule.Types)
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .Select(MapType)
                .ToList()
                .AsReadOnly();

            return new AssemblyInfo
            {
                Name = assembly.Name.Name,
                Version = assembly.Name.Version.ToString(),
                TargetFramework = targetFramework,
                PublicTypes = publicTypes,
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AssemblyInspector] Could not read assembly: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Yields publicly visible types, including public types nested inside other public types
    /// (named "Outer/Inner"). Compiler-generated types, whose names start with '&lt;', are skipped.
    /// </summary>
    private static IEnumerable<TypeDefinition> EnumeratePublicTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            if (!(type.IsPublic || type.IsNestedPublic) || type.Name.StartsWith('<'))
                continue;

            yield return type;

            foreach (var nested in EnumeratePublicTypes(type.NestedTypes))
                yield return nested;
        }
    }

    private static AssemblyTypeInfo MapType(TypeDefinition type)
    {
        var methods = type.Methods
            .Where(m => m.IsPublic && !m.IsSpecialName)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .Select(MapMethod)
            .ToList()
            .AsReadOnly();

        var properties = type.Properties
            .Where(p => p.GetMethod?.IsPublic == true || p.SetMethod?.IsPublic == true)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(MapProperty)
            .ToList()
            .AsReadOnly();

        var fields = type.Fields
            .Where(f => f.IsPublic && !f.IsRuntimeSpecialName)
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(MapField)
            .ToList()
            .AsReadOnly();

        return new AssemblyTypeInfo
        {
            FullName = type.FullName,
            Kind = GetTypeKind(type),
            BaseType = GetBaseType(type),
            Interfaces = type.Interfaces
                .Select(i => i.InterfaceType.FullName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly(),
            Modifiers = GetTypeModifiers(type).AsReadOnly(),
            Methods = methods,
            Properties = properties,
            Fields = fields,
        };
    }

    private static string GetTypeKind(TypeDefinition type)
    {
        if (type.IsEnum) return "enum";
        if (type.IsInterface) return "interface";
        if (IsDelegate(type)) return "delegate";
        if (type.IsValueType) return "struct";
        return "class";
    }

    private static bool IsDelegate(TypeDefinition type)
        => type.BaseType?.FullName is "System.Delegate" or "System.MulticastDelegate";

    private static string? GetBaseType(TypeDefinition type)
    {
        if (type.IsEnum || type.IsInterface || type.IsValueType || IsDelegate(type))
            return null;
        var bt = type.BaseType?.FullName;
        return bt is null or "System.Object" ? null : bt;
    }

    private static List<string> GetTypeModifiers(TypeDefinition type)
    {
        var mods = new List<string>();

        if (!type.IsEnum && !type.IsInterface && !type.IsValueType && !IsDelegate(type))
        {
            // abstract + sealed == static class in C#
            if (type.IsAbstract && type.IsSealed)
                mods.Add("static");
            else
            {
                if (type.IsAbstract) mods.Add("abstract");
                if (type.IsSealed) mods.Add("sealed");
            }
        }

        if (type.HasGenericParameters) mods.Add("generic");
        return mods;
    }

    private static AssemblyMethodInfo MapMethod(MethodDefinition method)
    {
        var mods = new List<string>();
        if (method.IsStatic) mods.Add("static");
        if (method.IsAbstract) mods.Add("abstract");
        else if (method.IsVirtual && !method.IsNewSlot) mods.Add("override");
        else if (method.IsVirtual) mods.Add("virtual");

        return new AssemblyMethodInfo
        {
            Name = method.Name,
            ReturnType = method.ReturnType.FullName,
            Parameters = method.Parameters
                .Select(p => $"{p.ParameterType.FullName} {p.Name}")
                .ToList()
                .AsReadOnly(),
            Modifiers = mods.AsReadOnly(),
        };
    }

    private static AssemblyPropertyInfo MapProperty(PropertyDefinition prop)
        => new()
        {
            Name = prop.Name,
            Type = prop.PropertyType.FullName,
            HasGetter = prop.GetMethod?.IsPublic == true,
            HasSetter = prop.SetMethod?.IsPublic == true,
        };

    private static AssemblyFieldInfo MapField(FieldDefinition field)
        => new()
        {
            Name = field.Name,
            Type = field.FieldType.FullName,
            IsStatic = field.IsStatic,
            IsReadOnly = field.IsInitOnly,
            IsConst = field.HasConstant,
            ConstValue = field.HasConstant ? field.Constant?.ToString() : null,
        };
}
