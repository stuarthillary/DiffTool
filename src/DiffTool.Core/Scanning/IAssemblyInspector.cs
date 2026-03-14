using DiffTool.Core.Models;

namespace DiffTool.Core.Scanning;

/// <summary>
/// Extracts .NET assembly metadata from a stream.
/// </summary>
public interface IAssemblyInspector
{
    /// <summary>
    /// Attempts to read assembly metadata from <paramref name="stream"/>.
    /// Returns null if the stream is not a valid .NET assembly.
    /// </summary>
    AssemblyInfo? TryInspect(Stream stream);
}
