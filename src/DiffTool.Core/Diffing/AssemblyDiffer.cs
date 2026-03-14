using DiffTool.Core.Models;

namespace DiffTool.Core.Diffing;

/// <summary>
/// Computes the API-surface diff between two <see cref="AssemblyInfo"/> instances.
/// Uses sorted merge-join at the type level and member level for O(n+m) performance.
///
/// Keying rules:
///   Methods   — name + "(" + comma-joined parameter types + ")" — handles overloads and
///               treats signature changes (return type aside) as remove + add pairs.
///   Properties — name + ":" + type — a property type change surfaces as remove + add.
///   Fields     — name + ":" + type — same as properties.
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

        // Both type lists are pre-sorted by FullName from AssemblyInspector.
        int li = 0, ri = 0;
        while (li < left.PublicTypes.Count || ri < right.PublicTypes.Count)
        {
            var leftType  = li < left.PublicTypes.Count  ? left.PublicTypes[li]  : null;
            var rightType = ri < right.PublicTypes.Count ? right.PublicTypes[ri] : null;

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

        var (addedMethods,   removedMethods)   = MergeJoin(leftMethods,  rightMethods,  MethodKey);
        var (addedProps,     removedProps)      = MergeJoin(leftProps,    rightProps,    PropertyKey);
        var (addedFields,    removedFields)     = MergeJoin(leftFields,   rightFields,   FieldKey);

        return new AssemblyTypeDiff
        {
            FullName          = left.FullName,
            AddedMethods      = addedMethods.AsReadOnly(),
            RemovedMethods    = removedMethods.AsReadOnly(),
            AddedProperties   = addedProps.AsReadOnly(),
            RemovedProperties = removedProps.AsReadOnly(),
            AddedFields       = addedFields.AsReadOnly(),
            RemovedFields     = removedFields.AsReadOnly(),
        };
    }

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
    /// Items whose keys match are treated as unchanged and discarded.
    /// </summary>
    private static (List<T> added, List<T> removed) MergeJoin<T>(
        List<T> left, List<T> right, Func<T, string> keySelector)
    {
        var added   = new List<T>();
        var removed = new List<T>();
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
            else              { li++; ri++; } // same key — unchanged
        }

        return (added, removed);
    }
}
