using System.Text;
using System.Text.Json;
using DiffTool.Core.Models;

namespace DiffTool.Cli.Tests;

public sealed class CliAppTests
{
    private sealed class CliRun : IDisposable
    {
        private readonly MemoryStream _binaryStdout = new();
        private readonly StringWriter _stdout = new();
        private readonly StringWriter _stderr = new();

        public int ExitCode { get; }
        public string Stdout => _stdout.ToString();
        public string Stderr => _stderr.ToString();
        public string BinaryStdout => Encoding.UTF8.GetString(_binaryStdout.ToArray());

        public CliRun(params string[] args)
        {
            // Hand out a non-closing wrapper so the captured bytes survive the command disposing its stream.
            var app = new CliApp(() => new NonClosingStream(_binaryStdout), _stdout, _stderr);
            ExitCode = app.Run(args);
        }

        public void Dispose() => _binaryStdout.Dispose();
    }

    private sealed class NonClosingStream(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.WriteAsync(buffer, cancellationToken);
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiffToolCliTests-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public string PathOf(string name) => System.IO.Path.Combine(Path, name);

        public string Write(string name, string content)
        {
            var full = PathOf(name);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            System.IO.File.WriteAllText(full, content);
            return full;
        }

        /// <summary>Copies <paramref name="source"/> to <paramref name="name"/>, creating parent directories.</summary>
        public string Copy(string source, string name)
        {
            var full = PathOf(name);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            System.IO.File.Copy(source, full);
            return full;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }

    // ---- snapshot ------------------------------------------------------------

    [Test]
    public async Task Snapshot_MissingDirectory_ReturnsOneAndReportsError()
    {
        using var temp = new TempDir();
        using var run = new CliRun("snapshot", temp.PathOf("nope"));

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stderr).Contains("does not exist");
    }

    [Test]
    public async Task Snapshot_WithOutputFile_WritesSnapshotAndConfirmation()
    {
        using var temp = new TempDir();
        temp.Write("tree/a.txt", "hello");
        var output = temp.PathOf("snap.json");

        using var run = new CliRun("snapshot", temp.PathOf("tree"), "--output", output);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Stdout).Contains("Snapshot saved to");
        using var doc = JsonDocument.Parse(File.ReadAllText(output));
        await Assert.That(doc.RootElement.GetProperty("entries").GetArrayLength()).IsEqualTo(1);
        await Assert.That(doc.RootElement.GetProperty("entries")[0].GetProperty("relativePath").GetString()).IsEqualTo("a.txt");
    }

    [Test]
    public async Task Snapshot_WithoutOutput_WritesJsonToStdoutStream()
    {
        using var temp = new TempDir();
        temp.Write("tree/a.txt", "hello");

        using var run = new CliRun("snapshot", temp.PathOf("tree"));

        await Assert.That(run.ExitCode).IsEqualTo(0);
        using var doc = JsonDocument.Parse(run.BinaryStdout);
        await Assert.That(doc.RootElement.GetProperty("entries").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task Snapshot_WithoutInspectAssemblies_OmitsAssemblyInfo()
    {
        using var temp = new TempDir();
        temp.Copy(typeof(CliApp).Assembly.Location, "tree/self.dll");

        using var run = new CliRun("snapshot", temp.PathOf("tree"));

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.BinaryStdout).DoesNotContain("assemblyInfo");
    }

    [Test]
    public async Task Snapshot_WithInspectAssemblies_IncludesAssemblyInfo()
    {
        using var temp = new TempDir();
        temp.Copy(typeof(CliApp).Assembly.Location, "tree/self.dll");

        using var run = new CliRun("snapshot", temp.PathOf("tree"), "--inspect-assemblies");

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.BinaryStdout).Contains("assemblyInfo");
        await Assert.That(run.BinaryStdout).Contains("DiffTool.CliApp");
    }

    // ---- diff ----------------------------------------------------------------

    private static (string Left, string Right) MakeSnapshots(TempDir temp)
    {
        temp.Write("v1/same.txt", "same");
        temp.Write("v1/changed.txt", "old");
        temp.Write("v1/removed.txt", "gone");
        temp.Write("v2/same.txt", "same");
        temp.Write("v2/changed.txt", "new content");
        temp.Write("v2/added.txt", "fresh");

        var left = temp.PathOf("left.json");
        var right = temp.PathOf("right.json");
        using (new CliRun("snapshot", temp.PathOf("v1"), "-o", left)) { }
        using (new CliRun("snapshot", temp.PathOf("v2"), "-o", right)) { }
        return (left, right);
    }

    [Test]
    public async Task Diff_MissingLeft_ReturnsOne()
    {
        using var temp = new TempDir();
        var (_, right) = MakeSnapshots(temp);

        using var run = new CliRun("diff", temp.PathOf("missing.json"), right);

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stderr).Contains("left snapshot file");
    }

    [Test]
    public async Task Diff_MissingRight_ReturnsOne()
    {
        using var temp = new TempDir();
        var (left, _) = MakeSnapshots(temp);

        using var run = new CliRun("diff", left, temp.PathOf("missing.json"));

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stderr).Contains("right snapshot file");
    }

    [Test]
    public async Task Diff_CorruptSnapshot_ReturnsOneWithoutThrowing()
    {
        using var temp = new TempDir();
        var (left, _) = MakeSnapshots(temp);
        var corrupt = temp.Write("corrupt.json", "{ not json");

        using var run = new CliRun("diff", left, corrupt);

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stderr).Contains("Error:");
    }

    [Test]
    public async Task Diff_ToStdout_JsonIsNotMixedWithSummary()
    {
        using var temp = new TempDir();
        var (left, right) = MakeSnapshots(temp);

        using var run = new CliRun("diff", left, right);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        // The stdout stream must be parseable JSON on its own; the summary goes to stderr.
        using var doc = JsonDocument.Parse(run.BinaryStdout);
        await Assert.That(doc.RootElement.GetProperty("summary").GetProperty("added").GetInt32()).IsEqualTo(1);
        await Assert.That(run.Stdout).DoesNotContain("added");
        await Assert.That(run.Stderr).Contains("1 added, 1 removed, 1 modified");
    }

    [Test]
    public async Task Diff_WithOutputFile_WritesDiffAndConfirmation()
    {
        using var temp = new TempDir();
        var (left, right) = MakeSnapshots(temp);
        var output = temp.PathOf("diff.json");

        using var run = new CliRun("diff", left, right, "-o", output);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Stdout).Contains("Diff saved to");
        using var doc = JsonDocument.Parse(File.ReadAllText(output));
        await Assert.That(doc.RootElement.GetProperty("summary").GetProperty("removed").GetInt32()).IsEqualTo(1);
    }

    // ---- report --------------------------------------------------------------

    private static string MakeDiffFile(TempDir temp)
    {
        var (left, right) = MakeSnapshots(temp);
        var diff = temp.PathOf("diff.json");
        using (new CliRun("diff", left, right, "-o", diff)) { }
        return diff;
    }

    [Test]
    public async Task Report_MissingDiffFile_ReturnsOne()
    {
        using var temp = new TempDir();

        using var run = new CliRun("report", temp.PathOf("missing.json"));

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stderr).Contains("diff file");
    }

    [Test]
    public async Task Report_MalformedDiffFile_ReturnsOneWithoutThrowing()
    {
        using var temp = new TempDir();
        var bad = temp.Write("bad.json", "{ not json");

        using var run = new CliRun("report", bad);

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stderr).Contains("Error:");
    }

    [Test]
    public async Task Report_NullJsonDocument_ReturnsOne()
    {
        using var temp = new TempDir();
        var nullJson = temp.Write("null.json", "null");

        using var run = new CliRun("report", nullJson);

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stderr).Contains("failed to deserialize");
    }

    [Test]
    public async Task Report_UnknownFormat_ReturnsOneAndListsSupportedFormats()
    {
        using var temp = new TempDir();
        var diff = MakeDiffFile(temp);

        using var run = new CliRun("report", diff, "--format", "pdf");

        await Assert.That(run.ExitCode).IsEqualTo(1);
        await Assert.That(run.Stderr).Contains("unknown report format 'pdf'");
        await Assert.That(run.Stderr).Contains("html");
    }

    [Test]
    public async Task Report_FormatIsCaseInsensitive()
    {
        using var temp = new TempDir();
        var diff = MakeDiffFile(temp);

        using var run = new CliRun("report", diff, "--format", "HTML");

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.BinaryStdout).Contains("<!DOCTYPE html>");
    }

    [Test]
    public async Task Report_DefaultFormatToStdout_WritesHtml()
    {
        using var temp = new TempDir();
        var diff = MakeDiffFile(temp);

        using var run = new CliRun("report", diff);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.BinaryStdout).Contains("added.txt");
        await Assert.That(run.BinaryStdout).Contains("removed.txt");
    }

    [Test]
    public async Task Report_WithOutputFile_WritesHtmlAndConfirmation()
    {
        using var temp = new TempDir();
        var diff = MakeDiffFile(temp);
        var output = temp.PathOf("report.html");

        using var run = new CliRun("report", diff, "-o", output);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.Stdout).Contains("Report saved to");
        await Assert.That(File.ReadAllText(output)).Contains("Directory Diff Report");
    }

    // ---- pipeline ------------------------------------------------------------

    [Test]
    public async Task Pipeline_AssemblyChangesSurviveSnapshotDiffReportRoundTrip()
    {
        using var temp = new TempDir();
        // Two different assemblies at the same path: the CLI assembly and the Core assembly.
        temp.Copy(typeof(CliApp).Assembly.Location, "v1/lib.dll");
        temp.Copy(typeof(DiffKind).Assembly.Location, "v2/lib.dll");
        var left = temp.PathOf("left.json");
        var right = temp.PathOf("right.json");
        var diff = temp.PathOf("diff.json");
        using (new CliRun("snapshot", temp.PathOf("v1"), "-a", "-o", left)) { }
        using (new CliRun("snapshot", temp.PathOf("v2"), "-a", "-o", right)) { }
        using (new CliRun("diff", left, right, "-o", diff)) { }

        using var run = new CliRun("report", diff);

        await Assert.That(run.ExitCode).IsEqualTo(0);
        await Assert.That(run.BinaryStdout).Contains("Assembly changes");
        await Assert.That(run.BinaryStdout).Contains("DiffTool.Core.Models.FileEntry");
    }

    // ---- parsing -------------------------------------------------------------

    [Test]
    public async Task UnknownCommand_ReturnsNonZero()
    {
        using var run = new CliRun("frobnicate");

        await Assert.That(run.ExitCode).IsNotEqualTo(0);
    }
}
