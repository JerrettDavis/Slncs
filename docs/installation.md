# Installation

This page covers how to acquire the Slncs components in both **wrapper** and **pure script** workflows.

## Components
| Component | Purpose | Delivery |
|-----------|---------|----------|
| `Slncs.Sdk` | MSBuild SDK (tasks + targets) that turns a C# script into `.slnx` and builds referenced projects. | NuGet (msbuild-sdks mapping via `global.json` or direct restore) |
| `Slncs.Tool` (`slncs-build`) | Dotnet tool that generates a transient wrapper for a pure `.slncs` C# file. | Local build / future NuGet tool package |
| `SlncsGen.dll` | Roslyn scripting host copied into SDK `tools/` for execution. | Built with repository | 

## Prerequisites
- .NET 8.0 or later SDK (9.x supported).
- Git (for repository workflow).
- Optional: Code coverage / CI tooling (Codecov, ReportGenerator) if integrating into pipelines.

## Option 1: Local Repository Build
```bash
# Clone
git clone https://github.com/<you>/Slncs.git
cd Slncs

# Build everything (generator copies into SDK tools directory)
dotnet build -c Release
```

Add `global.json` (if not already present) mapping the SDK version you just built/published:
```json
{
  "msbuild-sdks": {
    "Slncs.Sdk": "0.1.20"
  }
}
```

## Option 2: Local Feed / Packing
Pack and add to a local feed directory:
```bash
dotnet pack src/Slncs.Sdk -c Release -o ./local-packages
# (Optional) Pack the tool
dotnet pack src/Slncs.Tool -c Release -o ./local-packages
```
Reference via `nuget.config` (add a local package source) or by adding the SDK version to `global.json` (NuGet fallback folder will resolve it if available).

## Installing the Tool (Development Flow)
Install from local folder:
```bash
dotnet tool install --global src/Slncs.Tool --add-source ./local-packages
# or update later
dotnet tool update --global src/Slncs.Tool --add-source ./local-packages
```
Verify:
```bash
slncs-build --help
```

## Consuming the SDK (Wrapper Mode)
Create a wrapper file `MySolution.slncs`:
```xml
<Project Sdk="Slncs.Sdk">
  <PropertyGroup>
    <SlncsFile>MySolution.slncs.cs</SlncsFile>
    <GeneratedSlnx>obj/MySolution.slnx</GeneratedSlnx>
  </PropertyGroup>
</Project>
```
Add script `MySolution.slncs.cs` and build:
```bash
dotnet build MySolution.slncs
```

## Consuming the Tool (Pure Script Mode)
Create a single file `MySolution.slncs`:
```csharp
using Slncs;
Solution.Create().Project("src/App/App.csproj").Write(OutputPath);
```
Build:
```bash
slncs-build MySolution.slncs
```

## Upgrading
1. Update `global.json` SDK version.
2. Rebuild `SlncsGen` if pulling from source.
3. Clear any artifacts if semantic changes occurred.

## Verification Checklist
| Check | Command | Expected |
|-------|---------|----------|
| SDK resolves | `dotnet build MySolution.slncs -v:n` | Task messages mention `SlncsExec` / `SlnxParse` |
| Tool works | `slncs-build MySolution.slncs` | Creates `obj/MySolution.slnx` |
| Aggregator exists | Build completes | `obj/MySolution.slnx.proj` present (if projects found) |

Continue: [Getting Started](getting-started.md)
