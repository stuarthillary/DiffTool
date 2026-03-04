using DiffTool.Core.Models;

namespace DiffTool.Reports;

/// <summary>
/// Renders a snapshot diff as a standalone HTML report.
/// </summary>
public sealed class HtmlReportFormatter : IReportFormatter
{
    /// <inheritdoc />
    public string FormatName => "html";

    /// <inheritdoc />
    public string DefaultFileExtension => ".html";

    /// <inheritdoc />
    public Task RenderAsync(SnapshotDiff diff, Stream output, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
