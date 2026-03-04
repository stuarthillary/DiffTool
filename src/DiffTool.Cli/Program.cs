using System.CommandLine;

namespace DiffTool.Cli;

/// <summary>
/// Entry point for the DiffTool CLI.
/// Wires up System.CommandLine with snapshot, diff, and report subcommands.
/// </summary>
public static class Program
{
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
            Console.WriteLine($"[stub] snapshot: {directory?.FullName} -> {output?.FullName ?? "stdout"}");
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
            Console.WriteLine($"[stub] diff: {left?.FullName} vs {right?.FullName} -> {output?.FullName ?? "stdout"}");
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
            Console.WriteLine($"[stub] report: {diffFile?.FullName} --format {format} -> {output?.FullName ?? "stdout"}");
        });

        return command;
    }
}
