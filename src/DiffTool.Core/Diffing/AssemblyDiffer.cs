using DiffTool.Core.Models;

namespace DiffTool.Core.Diffing;

/// <summary>
/// Computes the API-surface diff between two <see cref="AssemblyInfo"/> instances.
/// Uses sorted merge-join at the type level and member level for O(n+m) performance.
///
/// Keying rules:
///   Methods   — name + "(" + comma-joined parameter types + ")" — handles overloads and
///               treats parameter-type changes as remove + add pairs.
///   Properties — name + ":" + type — a property type change surfaces as remove + add.
///   Fields     — name + ":" + type — same as properties.
///
/// Members that share a key but differ in other ways (return type, modifiers, accessors, constant
/// values) are reported as modified members; type-level kind, base type, modifiers and interface
/// changes are reported on the type.
/// </summary>
public sealed class AssemblyDiffer
{
    /// <summary>
    /// Compares two assembly snapshots and returns the diff of their public API surfaces.
    /// </summary>
    public AssemblyDiff Compare(AssemblyInfo left, AssemblyInfo right)
    {
        var addedTypes   = new List<AssemblyTypeInfo>();
        var removedTypes = new List<AssemblyTypeInfo>();
        var modifiedTypes = new List<AssemblyTypeDiff>();

        // Re-sort ordinally: the merge-join below depends on it, and snapshots written by an
        // earlier version (or another tool) may have used a different ordering.
        var leftTypes  = left.PublicTypes.OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
        var rightTypes = right.PublicTypes.OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();

        int li = 0, ri = 0;
        while (li < leftTypes.Count || ri < rightTypes.Count)
        {
            var leftType  = li < leftTypes.Count  ? leftTypes[li]  : null;
            var rightType = ri < rightTypes.Count ? rightTypes[ri] : null;

            int cmp = leftType is null  ? 1
                    : rightType is null ? -1
                    : string.Compare(leftType.FullName, rightType.FullName, StringComparison.Ordinal);

            if (cmp < 0)
            {
                removedTypes.Add(leftType!);
                li++;
            }
            else if (cmp > 0)
            {
                addedTypes.Add(rightType!);
                ri++;
            }
            else
            {
                var typeDiff = CompareTypes(leftType!, rightType!);
                if (typeDiff.HasChanges)
                    modifiedTypes.Add(typeDiff);
                li++;
                ri++;
            }
        }

        return new AssemblyDiff
        {
            VersionChanged         = left.Version != right.Version,
            TargetFrameworkChanged = left.TargetFramework != right.TargetFramework,
            AddedTypes             = addedTypes.AsReadOnly(),
            RemovedTypes           = removedTypes.AsReadOnly(),
            ModifiedTypes          = modifiedTypes.AsReadOnly(),
        };
    }

    private static AssemblyTypeDiff CompareTypes(AssemblyTypeInfo left, AssemblyTypeInfo right)
    {
        // Re-sort by full signature key before merge-joining, because AssemblyInspector
        // orders methods by name only (insufficient when overloads exist).
        var leftMethods  = left.Methods.OrderBy(MethodKey,   StringComparer.Ordinal).ToList();
        var rightMethods = right.Methods.OrderBy(MethodKey,  StringComparer.Ordinal).ToList();
        var leftProps    = left.Properties.OrderBy(PropertyKey,  StringComparer.Ordinal).ToList();
        var rightProps   = right.Properties.OrderBy(PropertyKey, StringComparer.Ordinal).ToList();
        var leftFields   = left.Fields.OrderBy(FieldKey,   StringComparer.Ordinal).ToList();
        var rightFields  = right.Fields.OrderBy(FieldKey,  StringComparer.Ordinal).ToList();

        var methods = MergeJoin(leftMethods, rightMethods, MethodKey);
        var props   = MergeJoin(leftProps,   rightProps,   PropertyKey);
        var fields  = MergeJoin(leftFields,  rightFields,  FieldKey);

        var modifiedMembers = new List<string>();
        foreach (var (l, r) in methods.Matched)
            modifiedMembers.AddRange(DescribeMethodChanges(l, r));
        foreach (var (l, r) in props.Matched)
            modifiedMembers.AddRange(DescribePropertyChanges(l, r));
        foreach (var (l, r) in fields.Matched)
            modifiedMembers.AddRange(DescribeFieldChanges(l, r));

        var leftInterfaces  = left.Interfaces.ToHashSet(StringComparer.Ordinal);
        var rightInterfaces = right.Interfaces.ToHashSet(StringComparer.Ordinal);

        return new AssemblyTypeDiff
        {
            FullName          = left.FullName,
            AddedMethods      = methods.Added.AsReadOnly(),
            RemovedMethods    = methods.Removed.AsReadOnly(),
            AddedProperties   = props.Added.AsReadOnly(),
            RemovedProperties = props.Removed.AsReadOnly(),
            AddedFields       = fields.Added.AsReadOnly(),
            RemovedFields     = fields.Removed.AsReadOnly(),
            AddedInterfaces   = right.Interfaces.Where(i => !leftInterfaces.Contains(i)).Order(StringComparer.Ordinal).ToList().AsReadOnly(),
            RemovedInterfaces = left.Interfaces.Where(i => !rightInterfaces.Contains(i)).Order(StringComparer.Ordinal).ToList().AsReadOnly(),
            TypeChanges       = DescribeTypeChanges(left, right).AsReadOnly(),
            ModifiedMembers   = modifiedMembers.AsReadOnly(),
        };
    }

    // -------------------------------------------------------------------------
    // Detail comparison for same-key items
    // -------------------------------------------------------------------------

    private static List<string> DescribeTypeChanges(AssemblyTypeInfo left, AssemblyTypeInfo right)
    {
        var changes = new List<string>();
        if (left.Kind != right.Kind)
            changes.Add($"kind: {left.Kind} -> {right.Kind}");
        if (left.BaseType != right.BaseType)
            changes.Add($"base type: {left.BaseType ?? "(none)"} -> {right.BaseType ?? "(none)"}");
        if (!SameSet(left.Modifiers, right.Modifiers))
            changes.Add($"modifiers: {FormatModifiers(left.Modifiers)} -> {FormatModifiers(right.Modifiers)}");
        return changes;
    }

    private static IEnumerable<string> DescribeMethodChanges(AssemblyMethodInfo left, AssemblyMethodInfo right)
    {
        var name = MethodKey(left);
        if (left.ReturnType != right.ReturnType)
            yield return $"{name}: return type {left.ReturnType} -> {right.ReturnType}";
        if (!SameSet(left.Modifiers, right.Modifiers))
            yield return $"{name}: modifiers {FormatModifiers(left.Modifiers)} -> {FormatModifiers(right.Modifiers)}";
    }

    private static IEnumerable<string> DescribePropertyChanges(AssemblyPropertyInfo left, AssemblyPropertyInfo right)
    {
        if (left.HasGetter != right.HasGetter)
            yield return $"{left.Name}: getter {(right.HasGetter ? "added" : "removed")}";
        if (left.HasSetter != right.HasSetter)
            yield return $"{left.Name}: setter {(right.HasSetter ? "added" : "removed")}";
    }

    private static IEnumerable<string> DescribeFieldChanges(AssemblyFieldInfo left, AssemblyFieldInfo right)
    {
        if (left.IsStatic != right.IsStatic)
            yield return $"{left.Name}: {(right.IsStatic ? "now static" : "no longer static")}";
        if (left.IsReadOnly != right.IsReadOnly)
            yield return $"{left.Name}: {(right.IsReadOnly ? "now readonly" : "no longer readonly")}";
        if (left.IsConst != right.IsConst)
            yield return $"{left.Name}: {(right.IsConst ? "now const" : "no longer const")}";
        if (left.ConstValue != right.ConstValue)
            yield return $"{left.Name}: value {left.ConstValue ?? "(none)"} -> {right.ConstValue ?? "(none)"}";
    }

    private static bool SameSet(IReadOnlyList<string> a, IReadOnlyList<string> b)
        => a.Count == b.Count && a.Order(StringComparer.Ordinal).SequenceEqual(b.Order(StringComparer.Ordinal));

    private static string FormatModifiers(IReadOnlyList<string> modifiers)
        => modifiers.Count == 0 ? "(none)" : string.Join(" ", modifiers);

    // -------------------------------------------------------------------------
    // Key functions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Produces the full signature key for a method:
    /// "MethodName(ParamType1,ParamType2)".
    /// Parameter names are excluded — only types participate in the key.
    /// </summary>
    internal static string MethodKey(AssemblyMethodInfo m)
    {
        var paramTypes = m.Parameters.Select(ParamType);
        return $"{m.Name}({string.Join(",", paramTypes)})";
    }

    /// <summary>Extracts the type portion from a "TypeFullName paramName" parameter string.</summary>
    private static string ParamType(string param)
    {
        var lastSpace = param.LastIndexOf(' ');
        return lastSpace >= 0 ? param[..lastSpace] : param;
    }

    private static string PropertyKey(AssemblyPropertyInfo p) => $"{p.Name}:{p.Type}";
    private static string FieldKey(AssemblyFieldInfo f)       => $"{f.Name}:{f.Type}";

    // -------------------------------------------------------------------------
    // Generic sorted merge-join
    // -------------------------------------------------------------------------

    /// <summary>
    /// Performs a sorted merge-join over two pre-sorted lists.
    /// Items present only in <paramref name="left"/> are returned as removed;
    /// items present only in <paramref name="right"/> are returned as added.
    /// Items whose keys match are returned as pairs so callers can compare the rest of their details.
    /// </summary>
    private static (List<T> Added, List<T> Removed, List<(T Left, T Right)> Matched) MergeJoin<T>(
        List<T> left, List<T> right, Func<T, string> keySelector)
    {
        var added   = new List<T>();
        var removed = new List<T>();
        var matched = new List<(T Left, T Right)>();
        int li = 0, ri = 0;

        while (li < left.Count || ri < right.Count)
        {
            var lk = li < left.Count  ? keySelector(left[li])  : null;
            var rk = ri < right.Count ? keySelector(right[ri]) : null;

            int cmp = lk is null ? 1
                    : rk is null ? -1
                    : string.Compare(lk, rk, StringComparison.Ordinal);

            if (cmp < 0)      removed.Add(left[li++]);
            else if (cmp > 0) added.Add(right[ri++]);
            else              matched.Add((left[li++], right[ri++]));
        }

        return (added, removed, matched);
    }
}
