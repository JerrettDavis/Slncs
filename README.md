# Slncs


Fluent, strongly-typed C# DSL for describing a .NET "solution" and generating a compact **`.slnx`** file that MSBuild can parse to build all referenced projects. Provides two consumption modes:

1. Wrapper project (XML) referencing a C# script (current / stable) — build with `dotnet build MySolution.slncs`.
2. Pure single-file C# script (`.slncs`) — build with the helper tool `slncs-build` (no manual wrapper needed).

The generator produces a `.slnx` file plus an optional aggregator MSBuild project (`.slnx.proj`) that references all discovered projects for convenient bulk operations.

---
## Contents
- [Quick Start](#quick-start)
  - [Wrapper Project Mode](#wrapper-project-mode)
  - [Pure Script Mode (`slncs-build`)](#pure-script-mode-slncs-build)
- [DSL Overview](#dsl-overview)
- [Tooling](#tooling)
- [Generated Artifacts](#generated-artifacts)
- [How It Works](#how-it-works)
- [Examples](#examples)
- [Incremental / Idempotent Behavior](#incremental--idempotent-behavior)
- [CLI & Tool Commands](#cli--tool-commands)
- [Local Development](#local-development)
- [Testing](#testing)
- [Roadmap](#roadmap)
- [License](#license)

---
## Quick Start

### Wrapper Project Mode
Suitable when you want to invoke `dotnet build` directly on the wrapper (no extra tooling).

```
MySolution.slncs          (XML wrapper MSBuild project)
MySolution.slncs.cs       (C# DSL script)
```

`MySolution.slncs`:
```xml
<Project Sdk="Slncs.Sdk">
  <PropertyGroup>
    <SlncsFile>MySolution.slncs.cs</SlncsFile>
    <GeneratedSlnx>obj\MySolution.slnx</GeneratedSlnx>
  </PropertyGroup>
</Project>
```

`MySolution.slncs.cs`:
```csharp
using Slncs;

Solution.Create()
    .Folder("/Solution Items", f => f.Files("Directory.Build.props"))
    .Project("src/ClassLibrary1/ClassLibrary1.csproj")
    .Project("src/ConsoleApp1/ConsoleApp1.csproj")
    .Write(OutputPath); // OutputPath provided by generator host
```

Build it:
```bash
dotnet build MySolution.slncs -v:m
```

### Pure Script Mode (`slncs-build`)
No wrapper XML; you keep only `MySolution.slncs` (C# code). The tool creates a transient wrapper under `obj/.slncs-build`.

`MySolution.slncs` (note: still C# code, just a different extension):
```csharp
using Slncs;

Solution.Create()
    .Project("src/App/App.csproj")
    .Write(OutputPath);
```
Build with the tool (after building or installing it):
```bash
# Run from repo (development):
dotnet run --project Slncs.Tool -- MySolution.slncs

# Or after packing & installing as a global/local tool:
slncs-build MySolution.slncs
```

The generated `.slnx` ends up at `obj/MySolution.slnx`.

---
## DSL Overview
The core entry point:
```csharp
Solution.Create()
    .Project("relative/path/to/Project.csproj")
    .Folder("/Solution Items", f => f.Files("README.md", "Directory.Build.props"))
    .Write(OutputPath);
```

Fluent members:
- `Project(string path)` — Add a project (relative recommended; duplicates coalesced).
- `Folder(string name, Action<FolderBuilder>)` — Add a logical folder (name normalized to include trailing '/').
- `FolderBuilder.File(string path)` / `Files(params string[] paths)` — Add file entries inside a folder.
- Nested folders via `FolderBuilder.Folder(...)`.
- `Write(string path)` (internal use: you typically call `.Write(OutputPath)` inside the script). If the provided path does not end in `.slnx` it is appended.

Output XML structure (simplified):
```xml
<Solution>
  <Folder Name="/Solution Items/">
    <File Path="Directory.Build.props" />
  </Folder>
  <Project Path="src/ClassLibrary1/ClassLibrary1.csproj" />
  <Project Path="src/ConsoleApp1/ConsoleApp1.csproj" />
</Solution>
```

Sorting & de-duplication:
- Folders (SortKey prefix `0|`)
- Projects (`1|`)
- Files (`2|`)
- Within each group lexicographical ordering ensures deterministic output.

---
## Tooling
### `slncs-build` (dotnet tool)
Generates a temporary wrapper project for a pure `.slncs` script, then calls `dotnet build` on it. The transient wrapper lives at `obj/.slncs-build/<Name>.wrapper.slncs` so that the repo's `global.json` pin (msbuild-sdks) applies.

Exit codes:
- `0` success
- `2` invalid path or missing file
- Other: underlying build failure

### Slncs MSBuild SDK
Imported via `<Project Sdk="Slncs.Sdk">`. It provides two internal tasks:
- `SlncsExec` — runs the generator to produce `.slnx` + optional aggregator.
- `SlnxParse` — reads `.slnx` and yields project paths for subsequent `MSBuild` invocation.

Generated aggregator (if any): `<YourSolution>.slnx.proj` referencing all discovered projects via `<ProjectReference/>`.

---
## Generated Artifacts
| Artifact | Purpose |
|----------|---------|
| `obj/<Name>.slnx` | Canonical solution description used to enumerate projects. |
| `obj/<Name>.slnx.proj` | Aggregator no-targets project referencing all projects (IDE load, bulk build). |
| `obj/.slncs-build/<Name>.wrapper.slncs` | Transient wrapper created only in pure-script mode. |

---
## How It Works
1. Your C# script (executed by Roslyn scripting) produces a `.slnx` by calling `Solution.Create()...Write(OutputPath)`.  
2. `SlncsExec` task captures that output, writes aggregator (optional) and returns control to MSBuild.  
3. `SlnxParse` enumerates `<Project Path="..."/>` elements for forwarding with `MSBuild` tasks (`Restore;Build`).

The `.slnx` format intentionally mirrors only essentials (projects, folders, loose files) — not a full .sln equivalent.

---
## Examples
Minimal:
```csharp
Solution.Create().Project("src/App/App.csproj").Write(OutputPath);
```
With folders & multiple projects:
```csharp
Solution.Create()
  .Folder("/Solution Items", f => f.Files("README.md", "Directory.Build.props"))
  .Project("src/Lib/Lib.csproj")
  .Project("src/Tool/Tool.csproj")
  .Write(OutputPath);
```

---
## Incremental / Idempotent Behavior
- Duplicate calls to `Project("A.csproj")` or identical file entries are coalesced.
- Ordering is stable for deterministic diff-friendly output.
- Re-running the script regenerates `.slnx`; aggregator is overwritten if contents change.

---
## CLI & Tool Commands
Build wrapper:
```bash
dotnet build MySolution.slncs
```
Build pure script:
```bash
slncs-build MySolution.slncs
```
Pack & install locally (development):
```bash
# From repo root
 dotnet pack Slncs.Sdk -c Release
 dotnet pack Slncs.Tool -c Release

# Optionally install tool (local feed or nupkg path)
 dotnet tool install --global --add-source ./local-packages Slncs.Tool
```

Run tests:
```bash
dotnet test -c Debug
```

---
## Local Development
Recommended workflow:
```bash
# 1. Clone
git clone <repo>
cd Slncs

# 2. Build everything (including tool + generator copied into SDK tools folder)
dotnet build

# 3. Run the wrapper sample (template)
dotnet build samples/template/MyCsSln.slncs

# 4. Run the pure script sample (no wrapper)
# Using the tool project directly (dev scenario):
dotnet run --project Slncs.Tool -- samples/pure/MyCsSln.slncs
# Or if you installed the tool globally:
slncs-build samples/pure/MyCsSln.slncs
```

---
## Testing
Test projects:
- `Slncs.Tests` — DSL & generator smoke tests.
- `Slncs.Sdk.Tests` — MSBuild task and runner tests.
- `E2E.Tests` — End-to-end wrapper and pure script build verifications.

All tests:
```bash
dotnet test
```

---
## Roadmap
- Optional caching / up-to-date checks for large solutions.
- Additional DSL helpers (globbing, conditional inclusion, grouping). 
- Rich CLI (list projects, diff two `.slnx` versions).
- Editor integration (Roslyn analyzers / IntelliSense improvements).

---
## License
[MIT](LICENSE)

