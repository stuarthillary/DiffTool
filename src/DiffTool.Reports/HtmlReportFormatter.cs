using System.Text;
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
    public async Task RenderAsync(SnapshotDiff diff, Stream output, CancellationToken cancellationToken = default)
    {
        var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            await WriteHtmlAsync(writer, diff, cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteHtmlAsync(StreamWriter w, SnapshotDiff diff, CancellationToken ct)
    {
        var sortedEntries = diff.Entries
            .OrderBy(e => KindSortOrder(e.Kind))
            .ThenBy(e => e.RelativePath, StringComparer.Ordinal)
            .ToList();

        var generated = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        await w.WriteAsync(HtmlDocStart(diff, generated)).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        // Summary cards
        await w.WriteAsync($$"""
            <div class="summary-grid">
              <div class="card card-added"><span class="count">{{diff.Summary.Added}}</span><span class="label">Added</span></div>
              <div class="card card-removed"><span class="count">{{diff.Summary.Removed}}</span><span class="label">Removed</span></div>
              <div class="card card-modified"><span class="count">{{diff.Summary.Modified}}</span><span class="label">Modified</span></div>
              <div class="card card-unchanged"><span class="count">{{diff.Summary.Unchanged}}</span><span class="label">Unchanged</span></div>
              <div class="card card-total"><span class="count">{{diff.Summary.Total}}</span><span class="label">Total</span></div>
            </div>
            """).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        // File table
        await w.WriteAsync("""
            <table>
              <thead>
                <tr>
                  <th scope="col">Status</th>
                  <th scope="col">Path</th>
                  <th scope="col">Size</th>
                  <th scope="col">Last Modified (UTC)</th>
                  <th scope="col">Changes</th>
                </tr>
              </thead>
              <tbody>
            """).ConfigureAwait(false);

        for (var i = 0; i < sortedEntries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = sortedEntries[i];
            var rowClass = i % 2 == 0 ? "row-even" : "row-odd";
            await w.WriteAsync(BuildEntryRow(entry, rowClass)).ConfigureAwait(false);
        }

        await w.WriteAsync("""
              </tbody>
            </table>
            </main>
            </body>
            </html>
            """).ConfigureAwait(false);
    }

    private static string HtmlDocStart(SnapshotDiff diff, string generated) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Directory Diff Report</title>
        <style>
          *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
          body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; font-size: 14px; background: #f8fafc; color: #1e293b; line-height: 1.5; }
          main { max-width: 1200px; margin: 0 auto; padding: 2rem 1.5rem; }
          h1 { font-size: 1.75rem; font-weight: 700; color: #0f172a; margin-bottom: 0.25rem; }
          .meta { color: #64748b; font-size: 0.875rem; margin-bottom: 1.5rem; }
          .meta span { display: inline-block; margin-right: 1.5rem; }
          .meta strong { color: #334155; }
          .summary-grid { display: flex; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 2rem; }
          .card { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 0.875rem 1.25rem; border-radius: 8px; min-width: 90px; border: 1px solid transparent; }
          .card .count { font-size: 1.75rem; font-weight: 700; line-height: 1; }
          .card .label { font-size: 0.75rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; margin-top: 0.25rem; }
          .card-added { background: #f0fdf4; border-color: #bbf7d0; color: #15803d; }
          .card-removed { background: #fef2f2; border-color: #fecaca; color: #b91c1c; }
          .card-modified { background: #fffbeb; border-color: #fde68a; color: #b45309; }
          .card-unchanged { background: #f8fafc; border-color: #e2e8f0; color: #64748b; }
          .card-total { background: #f1f5f9; border-color: #cbd5e1; color: #334155; }
          table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.06); }
          thead { background: #f1f5f9; }
          th { padding: 0.75rem 1rem; text-align: left; font-weight: 600; color: #475569; font-size: 0.8125rem; text-transform: uppercase; letter-spacing: 0.04em; border-bottom: 1px solid #e2e8f0; }
          td { padding: 0.625rem 1rem; border-bottom: 1px solid #f1f5f9; vertical-align: top; }
          tr:last-child td { border-bottom: none; }
          .row-even { background: #fff; }
          .row-odd { background: #fafafa; }
          .badge { display: inline-block; padding: 0.2rem 0.55rem; border-radius: 9999px; font-size: 0.75rem; font-weight: 700; white-space: nowrap; }
          .badge-added { background: #dcfce7; color: #15803d; }
          .badge-removed { background: #fee2e2; color: #b91c1c; }
          .badge-modified { background: #fef3c7; color: #b45309; }
          .badge-unchanged { background: #f1f5f9; color: #64748b; }
          .path { font-family: "SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace; font-size: 0.8125rem; color: #1e293b; word-break: break-all; }
          .size { color: #475569; white-space: nowrap; }
          .date { color: #64748b; white-space: nowrap; font-size: 0.8125rem; }
          .changes { color: #78716c; font-size: 0.75rem; font-style: italic; }
          footer { margin-top: 1.5rem; color: #94a3b8; font-size: 0.75rem; text-align: center; }
        </style>
        </head>
        <body>
        <main>
        <h1>Directory Diff Report</h1>
        <div class="meta">
          <span><strong>Left path:</strong> {{Escape(diff.LeftPath)}}</span>
          <span><strong>Left snapshot:</strong> {{diff.LeftCapturedAtUtc:yyyy-MM-dd HH:mm:ss}} UTC</span>
          <span><strong>Right path:</strong> {{Escape(diff.RightPath)}}</span>
          <span><strong>Right snapshot:</strong> {{diff.RightCapturedAtUtc:yyyy-MM-dd HH:mm:ss}} UTC</span>
          <span><strong>Generated:</strong> {{generated}}</span>
        </div>
        """;

    private static string BuildEntryRow(DiffEntry entry, string rowClass)
    {
        var (badgeClass, badgeText) = entry.Kind switch
        {
            DiffKind.Added     => ("badge-added",     "+ Added"),
            DiffKind.Removed   => ("badge-removed",   "- Removed"),
            DiffKind.Modified  => ("badge-modified",  "~ Modified"),
            DiffKind.Unchanged => ("badge-unchanged", "= Unchanged"),
            _                  => ("badge-unchanged", entry.Kind.ToString())
        };

        // Size: use Right for Added/Modified/Unchanged; use Left for Removed
        var fileEntry = entry.Kind == DiffKind.Removed ? entry.Left : entry.Right;
        var sizeText  = FormatSize(fileEntry?.SizeBytes);
        var dateText  = fileEntry?.LastModifiedUtc.ToString("yyyy-MM-dd HH:mm") ?? "—";

        var changesHtml = entry.ChangedProperties.Count > 0
            ? Escape(string.Join(", ", entry.ChangedProperties))
            : "";

        return $"""
            <tr class="{rowClass}">
              <td><span class="badge {badgeClass}">{badgeText}</span></td>
              <td class="path">{Escape(entry.RelativePath)}</td>
              <td class="size">{sizeText}</td>
              <td class="date">{dateText}</td>
              <td class="changes">{changesHtml}</td>
            </tr>
            """;
    }

    private static int KindSortOrder(DiffKind kind) => kind switch
    {
        DiffKind.Added     => 0,
        DiffKind.Removed   => 1,
        DiffKind.Modified  => 2,
        DiffKind.Unchanged => 3,
        _                  => 4
    };

    private static string FormatSize(long? bytes)
    {
        if (bytes is null) return "—";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.0} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):0.0} GB";
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
