using DiffTool.Core.Models;

namespace DiffTool.Reports;

/// <summary>
/// Plugin interface for rendering diff reports.
/// </summary>
public interface IReportFormatter
{
    /// <summary>
    /// Unique name of this format (e.g., "html", "markdown", "csv").
    /// Used as the --format argument value in the CLI.
    /// </summary>
    string FormatName { get; }

    /// <summary>
    /// The default file extension for output files (e.g., ".html").
    /// </summary>
    string DefaultFileExtension { get; }

    /// <summary>
    /// Renders a diff report to the output stream.
    /// </summary>
    /// <param name="diff">The snapshot diff to render.</param>
    /// <param name="output">Stream to write the report to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RenderAsync(SnapshotDiff diff, Stream output, CancellationToken cancellationToken = default);
}
