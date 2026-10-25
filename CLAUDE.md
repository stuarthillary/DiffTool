# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build entire solution
dotnet build DiffTool.slnx

# Run all tests
dotnet run --project tests/DiffTool.Core.Tests
dotnet run --project tests/DiffTool.Reports.Tests
dotnet run --project tests/DiffTool.Cli.Tests

# Run a single test by name (TUnit uses --treenode-filter)
dotnet run --project tests/DiffTool.Core.Tests -- --treenode-filter "*/SnapshotDifferTests/*"

# Run the CLI
dotnet run --project src/DiffTool.Cli -- snapshot <directory> [--output <file>]
dotnet run --project src/DiffTool.Cli -- diff <left.json> <right.json> [--output <file>]
dotnet run --project src/DiffTool.Cli -- report <diff.json> [--format html] [--output <file>]
```

**Build constraints:** `TreatWarningsAsErrors=true` — all warnings must be resolved. Nullable annotations are enforced.

**Test framework:** TUnit (not xUnit/NUnit). Tests are executable projects (`OutputType: Exe`), run with `dotnet run`. Assertions use `await Assert.That(...)` syntax.

## Architecture

DiffTool is a .NET 10 CLI for directory tree comparison. The pipeline is:

```
Directory → [DirectoryScanner] → DirectorySnapshot (JSON)
                                          ↓
             Snapshot A → [SnapshotDiffer] → SnapshotDiff (JSON)
             Snapshot B ↗                          ↓
                                         [IReportFormatter]
                                                   ↓
                                              Report file
```

**Projects:**
- `DiffTool.Core` — domain models, scanning, diffing, serialization
- `DiffTool.Cli` — `System.CommandLine` entry point with three subcommands: `snapshot`, `diff`, `report`
- `DiffTool.Reports` — report formatters (`IReportFormatter`); currently only `HtmlReportFormatter`

**Key abstractions:**
- `IFileSystem` — wraps `System.IO` for testable scanning; production impl is `PhysicalFileSystem`
- `ISnapshotSerializer` — pluggable snapshot format; only `JsonSnapshotSerializer` exists (camelCase, indented, nulls omitted)
- `IReportFormatter` — pluggable report format; `FormatName` maps to `--format` CLI arg

**Core models (all immutable records/sealed classes):**
- `FileEntry` — single file/dir metadata: `RelativePath` (forward-slash normalized), `Hash` (SHA-256 hex, null for dirs), `SizeBytes`, `LastModifiedUtc`, `IsDirectory`, `Attributes`
- `DirectorySnapshot` — ordered `IReadOnlyList<FileEntry>` sorted by `RelativePath` + `Version` field for schema evolution
- `DiffKind` enum — `Added | Removed | Modified | Unchanged`
- `DiffEntry` — `RelativePath`, `Kind`, `Left`/`Right` (`FileEntry?`), `ChangedProperties`
- `SnapshotDiff` — ordered `IReadOnlyList<DiffEntry>` + `DiffSummary` (counts per kind)

**Diffing algorithm:** `SnapshotDiffer` uses a merge-join over two sorted entry lists (O(n+m)). Hash equality → `Unchanged` regardless of timestamps. If hashes differ, the changed properties list is populated.

**Testing patterns:**
- `FakeFileSystem` (hand-rolled) replaces `IFileSystem` — prefer this over mocking libraries for multi-method interfaces
- Fixed `DateTimeOffset` values for deterministic timestamps
- Mocking library available: `Imposter`
