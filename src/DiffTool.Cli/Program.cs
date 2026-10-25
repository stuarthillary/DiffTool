using System.CommandLine;
using System.Text.Json;
using DiffTool.Core.Diffing;
using DiffTool.Core.Models;
using DiffTool.Core.Scanning;
using DiffTool.Core.Serialization;
using DiffTool.Reports;

namespace DiffTool.Cli;

/// <summary>
/// The DiffTool CLI. Wires up System.CommandLine with snapshot, diff, and report subcommands.
/// Output streams are injected so the commands can be tested without touching the process console.
/// </summary>
public sealed class CliApp
{
    private static readonly JsonSerializerOptions s_diffJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Func<Stream> _openStandardOutput;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    /// <param name="openStandardOutput">Opens the binary stdout stream used when <c>--output</c> is not given.</param>
    /// <param name="stdout">Text writer for informational messages and help.</param>
    /// <param name="stderr">Text writer for errors and the diff summary.</param>
    public CliApp(Func<Stream> openStandardOutput, TextWriter stdout, TextWriter stderr)
    {
        _openStandardOutput = openStandardOutput;
        _stdout = stdout;
        _stderr = stderr;
    }

    /// <summary>Runs the CLI and returns the process exit code.</summary>
    public int Run(string[] args)
    {
        var rootCommand = new RootCommand("DiffTool — scan directory trees, compare snapshots, generate drift reports.");

        rootCommand.Add(BuildSnapshotCommand());
        rootCommand.Add(BuildDiffCommand());
        rootCommand.Add(BuildReportCommand());

        var parseResult = rootCommand.Parse(args);
        return parseResult.Invoke(new InvocationConfiguration { Output = _stdout, Error = _stderr });
    }

    /// <summary>
    /// Runs a command body, converting expected I/O and format failures into an error message and exit code 1.
    /// </summary>
    private int Guard(Func<int> body)
    {
        try
        {
            return body();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            _stderr.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private int Fail(string message, int exitCode = 1)
    {
        _stderr.WriteLine(message);
        return exitCode;
    }

    private Command BuildSnapshotCommand()
    {
        var directoryArg = new Argument<DirectoryInfo>("directory")
        {
            Description = "The directory to scan."
        };

        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Output file path for the snapshot. Defaults to stdout."
        };

        var inspectAssembliesOption = new Option<bool>("--inspect-assemblies", "-a")
        {
            Description = "Capture public API metadata for .NET assemblies (.dll/.exe) so diffs can report API changes."
        };

        var command = new Command("snapshot", "Scan a directory tree and save a snapshot.");
        command.Add(directoryArg);
        command.Add(outputOption);
        command.Add(inspectAssembliesOption);

        command.SetAction(parseResult => Guard(() =>
        {
            var directory = parseResult.GetValue(directoryArg);
            var output = parseResult.GetValue(outputOption);
            var inspectAssemblies = parseResult.GetValue(inspectAssembliesOption);

            if (directory == null || !directory.Exists)
            {
                return Fail($"Error: directory '{directory?.FullName}' does not exist.");
            }

            var scanner = new DirectoryScanner(
                new PhysicalFileSystem(),
                inspectAssemblies ? new AssemblyInspector() : null);
            var snapshot = scanner.ScanAsync(directory.FullName, CancellationToken.None).GetAwaiter().GetResult();

            var serializer = new JsonSnapshotSerializer();

            if (output != null)
            {
                using var stream = output.Open(FileMode.Create, FileAccess.Write);
                serializer.SerializeAsync(snapshot, stream, CancellationToken.None).GetAwaiter().GetResult();
                _stdout.WriteLine($"Snapshot saved to {output.FullName}");
            }
            else
            {
                using var stream = _openStandardOutput();
                serializer.SerializeAsync(snapshot, stream, CancellationToken.None).GetAwaiter().GetResult();
            }

            return 0;
        }));

        return command;
    }

    private Command BuildDiffCommand()
    {
        var leftArg = new Argument<FileInfo>("left")
        {
            Description = "Path to the left (older/baseline) snapshot file."
        };

        var rightArg = new Argument<FileInfo>("right")
        {
            Description = "Path to the right (newer/current) snapshot file."
        };

        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Output file path for the diff result. Defaults to stdout."
        };

        var command = new Command("diff", "Compare two snapshots and produce a diff.");
        command.Add(leftArg);
        command.Add(rightArg);
        command.Add(outputOption);

        command.SetAction(parseResult => Guard(() =>
        {
            var left = parseResult.GetValue(leftArg);
            var right = parseResult.GetValue(rightArg);
            var output = parseResult.GetValue(outputOption);

            if (left == null || !left.Exists)
            {
                return Fail($"Error: left snapshot file '{left?.FullName}' does not exist.");
            }

            if (right == null || !right.Exists)
            {
                return Fail($"Error: right snapshot file '{right?.FullName}' does not exist.");
            }

            var serializer = new JsonSnapshotSerializer();

            DirectorySnapshot leftSnapshot;
            DirectorySnapshot rightSnapshot;

            using (var leftStream = left.OpenRead())
                leftSnapshot = serializer.DeserializeAsync(leftStream, CancellationToken.None).GetAwaiter().GetResult();

            using (var rightStream = right.OpenRead())
                rightSnapshot = serializer.DeserializeAsync(rightStream, CancellationToken.None).GetAwaiter().GetResult();

            var differ = new SnapshotDiffer();
            var diff = differ.Compare(leftSnapshot, rightSnapshot);

            if (output != null)
            {
                using var stream = output.Open(FileMode.Create, FileAccess.Write);
                JsonSerializer.SerializeAsync(stream, diff, s_diffJsonOptions, CancellationToken.None).GetAwaiter().GetResult();
                _stdout.WriteLine($"Diff saved to {output.FullName}");
            }
            else
            {
                using var stream = _openStandardOutput();
                JsonSerializer.SerializeAsync(stream, diff, s_diffJsonOptions, CancellationToken.None).GetAwaiter().GetResult();
            }

            var summary = diff.Summary;
            // Stderr, so it cannot corrupt the diff JSON when that is written to stdout.
            _stderr.WriteLine($"{summary.Added} added, {summary.Removed} removed, {summary.Modified} modified, {summary.Unchanged} unchanged");
            return 0;
        }));

        return command;
    }

    private Command BuildReportCommand()
    {
        var diffArg = new Argument<FileInfo>("diff-file")
        {
            Description = "Path to a diff file (produced by the diff command)."
        };

        var formatOption = new Option<string>("--format", "-f")
        {
            Description = "Report output format.",
            DefaultValueFactory = _ => "html"
        };

        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Output file path for the report. Defaults to stdout."
        };

        var command = new Command("report", "Generate a report from a diff file.");
        command.Add(diffArg);
        command.Add(formatOption);
        command.Add(outputOption);

        command.SetAction(parseResult => Guard(() =>
        {
            var diffFile = parseResult.GetValue(diffArg);
            var format = parseResult.GetValue(formatOption);
            var output = parseResult.GetValue(outputOption);

            if (diffFile == null || !diffFile.Exists)
            {
                return Fail($"Error: diff file '{diffFile?.FullName}' does not exist.");
            }

            SnapshotDiff? diff;
            using (var diffStream = diffFile.OpenRead())
            {
                diff = JsonSerializer.DeserializeAsync<SnapshotDiff>(diffStream, s_diffJsonOptions, CancellationToken.None).GetAwaiter().GetResult();
            }

            if (diff == null)
            {
                return Fail($"Error: failed to deserialize diff file '{diffFile.FullName}'.");
            }

            IReportFormatter? formatter = format?.ToLowerInvariant() switch
            {
                "html" => new HtmlReportFormatter(),
                _ => null
            };

            if (formatter == null)
            {
                return Fail($"Error: unknown report format '{format}'. Supported formats: html.");
            }

            if (output != null)
            {
                using var stream = output.Open(FileMode.Create, FileAccess.Write);
                formatter.RenderAsync(diff, stream, CancellationToken.None).GetAwaiter().GetResult();
                _stdout.WriteLine($"Report saved to {output.FullName}");
            }
            else
            {
                using var stream = _openStandardOutput();
                formatter.RenderAsync(diff, stream, CancellationToken.None).GetAwaiter().GetResult();
            }

            return 0;
        }));

        return command;
    }
}

/// <summary>Process entry point.</summary>
public static class Program
{
    public static int Main(string[] args)
        => new CliApp(Console.OpenStandardOutput, Console.Out, Console.Error).Run(args);
}
