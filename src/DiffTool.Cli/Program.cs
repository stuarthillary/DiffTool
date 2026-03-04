using System.CommandLine;
using System.Text.Json;
using DiffTool.Core.Diffing;
using DiffTool.Core.Models;
using DiffTool.Core.Scanning;
using DiffTool.Core.Serialization;
using DiffTool.Reports;

namespace DiffTool.Cli;

/// <summary>
/// Entry point for the DiffTool CLI.
/// Wires up System.CommandLine with snapshot, diff, and report subcommands.
/// </summary>
public static class Program
{
    private static readonly JsonSerializerOptions s_diffJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand("DiffTool — scan directory trees, compare snapshots, generate drift reports.");

        rootCommand.Add(BuildSnapshotCommand());
        rootCommand.Add(BuildDiffCommand());
        rootCommand.Add(BuildReportCommand());

        var parseResult = rootCommand.Parse(args);
        return parseResult.Invoke();
    }

    private static Command BuildSnapshotCommand()
    {
        var directoryArg = new Argument<DirectoryInfo>("directory")
        {
            Description = "The directory to scan."
        };

        var outputOption = new Option<FileInfo?>("--output", "-o")
        {
            Description = "Output file path for the snapshot. Defaults to stdout."
        };

        var command = new Command("snapshot", "Scan a directory tree and save a snapshot.");
        command.Add(directoryArg);
        command.Add(outputOption);

        command.SetAction(parseResult =>
        {
            var directory = parseResult.GetValue(directoryArg);
            var output = parseResult.GetValue(outputOption);

            if (directory == null || !directory.Exists)
            {
                Console.Error.WriteLine($"Error: directory '{directory?.FullName}' does not exist.");
                Environment.ExitCode = 1;
                return;
            }

            var scanner = new DirectoryScanner(new PhysicalFileSystem());
            var snapshot = scanner.ScanAsync(directory.FullName, CancellationToken.None).GetAwaiter().GetResult();

            var serializer = new JsonSnapshotSerializer();

            if (output != null)
            {
                using var stream = output.Open(FileMode.Create, FileAccess.Write);
                serializer.SerializeAsync(snapshot, stream, CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine($"Snapshot saved to {output.FullName}");
            }
            else
            {
                using var stream = Console.OpenStandardOutput();
                serializer.SerializeAsync(snapshot, stream, CancellationToken.None).GetAwaiter().GetResult();
            }
        });

        return command;
    }

    private static Command BuildDiffCommand()
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

        command.SetAction(parseResult =>
        {
            var left = parseResult.GetValue(leftArg);
            var right = parseResult.GetValue(rightArg);
            var output = parseResult.GetValue(outputOption);

            if (left == null || !left.Exists)
            {
                Console.Error.WriteLine($"Error: left snapshot file '{left?.FullName}' does not exist.");
                Environment.ExitCode = 1;
                return;
            }

            if (right == null || !right.Exists)
            {
                Console.Error.WriteLine($"Error: right snapshot file '{right?.FullName}' does not exist.");
                Environment.ExitCode = 1;
                return;
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
                Console.WriteLine($"Diff saved to {output.FullName}");
            }
            else
            {
                using var stream = Console.OpenStandardOutput();
                JsonSerializer.SerializeAsync(stream, diff, s_diffJsonOptions, CancellationToken.None).GetAwaiter().GetResult();
            }

            var summary = diff.Summary;
            Console.WriteLine($"{summary.Added} added, {summary.Removed} removed, {summary.Modified} modified, {summary.Unchanged} unchanged");
        });

        return command;
    }

    private static Command BuildReportCommand()
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

        command.SetAction(parseResult =>
        {
            var diffFile = parseResult.GetValue(diffArg);
            var format = parseResult.GetValue(formatOption);
            var output = parseResult.GetValue(outputOption);

            if (diffFile == null || !diffFile.Exists)
            {
                Console.Error.WriteLine($"Error: diff file '{diffFile?.FullName}' does not exist.");
                Environment.ExitCode = 1;
                return;
            }

            SnapshotDiff? diff;
            using (var diffStream = diffFile.OpenRead())
            {
                diff = JsonSerializer.DeserializeAsync<SnapshotDiff>(diffStream, s_diffJsonOptions, CancellationToken.None).GetAwaiter().GetResult();
            }

            if (diff == null)
            {
                Console.Error.WriteLine($"Error: failed to deserialize diff file '{diffFile.FullName}'.");
                Environment.ExitCode = 1;
                return;
            }

            IReportFormatter? formatter = format?.ToLowerInvariant() switch
            {
                "html" => new HtmlReportFormatter(),
                _ => null
            };

            if (formatter == null)
            {
                Console.Error.WriteLine($"Error: unknown report format '{format}'. Supported formats: html.");
                Environment.ExitCode = 1;
                return;
            }

            try
            {
                if (output != null)
                {
                    using var stream = output.Open(FileMode.Create, FileAccess.Write);
                    formatter.RenderAsync(diff, stream, CancellationToken.None).GetAwaiter().GetResult();
                    Console.WriteLine($"Report saved to {output.FullName}");
                }
                else
                {
                    using var stream = Console.OpenStandardOutput();
                    formatter.RenderAsync(diff, stream, CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            catch (NotImplementedException)
            {
                Console.Error.WriteLine($"Report format '{format}' is not yet implemented.");
                Environment.ExitCode = 2;
            }
        });

        return command;
    }
}
