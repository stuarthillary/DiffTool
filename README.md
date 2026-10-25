# DiffTool

A .NET 10 command-line tool for detecting drift between directory trees. It records a directory as a **snapshot** (a JSON file of every file's path, size, timestamp, attributes and SHA-256 hash), **diffs** two snapshots, and renders the result as an HTML **report**.

Because snapshots are plain files, you can capture a directory on one machine, ship the JSON elsewhere, and compare it later against another machine or a later point in time, without needing access to both trees at once.

## Pipeline

```
Directory ──[snapshot]──▶ snapshot.json ─┐
                                         ├─[diff]──▶ diff.json ──[report]──▶ report.html
Directory ──[snapshot]──▶ snapshot.json ─┘
```

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
dotnet build DiffTool.slnx
```

## Usage

All commands are run through the CLI project (or the built `DiffTool.Cli` executable). Every command writes to stdout unless `--output` / `-o` is given.

### 1. Take a snapshot

```bash
dotnet run --project src/DiffTool.Cli -- snapshot <directory> [--output <file>] [--inspect-assemblies]
```

Walks the directory recursively and records, for each file and subdirectory: relative path (forward-slash normalized), SHA-256 hash (files only), size, last-modified time (UTC) and attributes. Files that cannot be read (I/O or permission errors) are recorded without a hash, keeping whatever size, timestamp and attributes could be read, instead of aborting the scan; directories that cannot be listed are skipped. Directory symlinks and junctions are listed as entries but not followed, so link cycles cannot loop. The snapshot also records the root path, capture time and machine name.

### 2. Diff two snapshots

```bash
dotnet run --project src/DiffTool.Cli -- diff <left.json> <right.json> [--output <file>]
```

`left` is the older/baseline snapshot and `right` the newer one. Each path is classified as:

| Kind | Meaning |
|------|---------|
| `Added` | Present only in the right snapshot |
| `Removed` | Present only in the left snapshot |
| `Modified` | Present in both with differing content or metadata |
| `Unchanged` | Present in both, identical |

Two files with the same hash are `Unchanged` even if their timestamps differ. For modified entries, `changedProperties` lists what differed (`Hash`, `SizeBytes`, `LastModifiedUtc`, `Attributes`, `IsDirectory`). Directories are compared on attributes only (their timestamp changes whenever a child does). A one-line summary (`N added, N removed, N modified, N unchanged`) is written to stderr, so piped JSON stays valid.

### 3. Generate a report

```bash
dotnet run --project src/DiffTool.Cli -- report <diff.json> [--format html] [--output <file>]
```

`--format` currently supports only `html` (the default).

### Example

```bash
dotnet run --project src/DiffTool.Cli -- snapshot ./release-1.0 -o before.json
dotnet run --project src/DiffTool.Cli -- snapshot ./release-1.1 -o after.json
dotnet run --project src/DiffTool.Cli -- diff before.json after.json -o diff.json
dotnet run --project src/DiffTool.Cli -- report diff.json -o report.html
```

## .NET assembly diffing

The core library can also compare the **public API surface** of `.dll`/`.exe` files using [Mono.Cecil](https://github.com/jbevain/cecil). When a scanner is given an `IAssemblyInspector`, each .NET assembly entry carries its name, version, target framework and public types (with their methods, properties and fields). When a modified assembly is diffed, the entry gets an `AssemblyDiff` listing added/removed types, added/removed members, and version or target-framework changes. Besides added/removed types and members, it reports changes to a type's kind, base type, modifiers and interfaces, and to a member's return type, modifiers, property accessors and constant values. Public nested types are included (named `Outer/Inner`); protected members and non-public types are not part of the compared surface. The HTML report includes this detail.

From the CLI, assembly metadata is opt-in: pass `--inspect-assemblies` (`-a`) to `snapshot`. Without it, snapshots contain no assembly metadata and assembly diffs are not produced.

```bash
dotnet run --project src/DiffTool.Cli -- snapshot ./release-1.0 --inspect-assemblies -o before.json
```

## Solution layout

| Project | Purpose |
|---------|---------|
| `src/DiffTool.Core` | Domain models, directory scanning, snapshot/assembly diffing, JSON serialization |
| `src/DiffTool.Reports` | Report formatters (`IReportFormatter`); currently `HtmlReportFormatter` |
| `src/DiffTool.Cli` | `System.CommandLine` entry point with the `snapshot`, `diff` and `report` commands |
| `tests/DiffTool.Core.Tests` | Tests for scanning, diffing and serialization |
| `tests/DiffTool.Reports.Tests` | Tests for the HTML formatter |
| `tests/DiffTool.Cli.Tests` | End-to-end tests for the CLI commands |

### Extension points

- `IFileSystem`: abstracts `System.IO` for scanning (`PhysicalFileSystem` in production, a hand-rolled fake in tests).
- `ISnapshotSerializer`: snapshot file format (currently `JsonSnapshotSerializer`: camelCase, indented, nulls omitted).
- `IReportFormatter`: report output format; its `FormatName` maps to the `--format` argument.
- `IAssemblyInspector`: extracts assembly metadata during scans.

### How diffing works

`SnapshotDiffer` merge-joins the two path-sorted entry lists in O(n + m). `AssemblyDiffer` does the same for types and members; methods are keyed on name plus parameter types, so a changed signature shows up as one removal and one addition.

## Testing

Tests use [TUnit](https://github.com/thomhurst/TUnit) and are executable projects, so they run with `dotnet run`:

```bash
dotnet run --project tests/DiffTool.Core.Tests
dotnet run --project tests/DiffTool.Reports.Tests
dotnet run --project tests/DiffTool.Cli.Tests

# a single test class
dotnet run --project tests/DiffTool.Core.Tests -- --treenode-filter "*/SnapshotDifferTests/*"
```

The build treats warnings as errors and enforces nullable annotations.
