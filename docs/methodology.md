# Methodology


Slncs follows a few guiding principles intended to keep the experiment focused and the learning signal high.

## Design Principles
| Principle | Explanation |
|-----------|------------|
| Single-language authoring | Keep solution graph definition in C# to minimize context switching. |
| Small surface area | Provide only primitives (Project, Folder, File) at first. Avoid early complexity. |
| Deterministic output | Stable ordering & de-duplication produce clean diffs & enable caching. |
| Transparent build pipeline | Generated artifacts (`.slnx`, aggregator) are plain text, inspectable. |
| Opt-in convenience | Aggregator project is optional; script alone defines the graph. |
| Reversible | No irreversible transformation of underlying project files. |
| Avoid magic | The DSL is a thin façade; no hidden reflection-based scanning beyond what the script directs. |

## Architectural Flow
```
C# Script (.slncs/.slncs.cs)
          │ Roslyn scripting host
          ▼
Generated .slnx (Solution XML) ──► Parse task (SlnxParse) ──► List<Project>
                                                       │
                                                       ▼
                                               MSBuild Restore+Build
```

## Determinism Strategy
- Key-based grouping: Folders (0), Projects (1), Files (2) sorted lexicographically.
- Duplicate elimination on key collision.
- Output encoding: UTF-8 with XML declaration.

## Extensibility Approach
Before adding DSL verbs, prefer user-land extension methods:
```csharp
public static class MySolutionExtensions
{
    public static SolutionBuilder AddBackend(this SolutionBuilder b)
        => b.Project("src/Core/Core.csproj")
             .Project("src/Api/Api.csproj");
}
```
This guards against premature expansion of core surface area.

## Security Posture
- Treat scripts as code (review path, no silent network I/O in core tasks).
- The generator performs no dynamic code emission beyond Roslyn scripting of the user file.

## Performance Notes
- Overhead dominated by Roslyn scripting startup for small graphs.
- Planned optimization: optional caching (hash of script -> .slnx) if unchanged.

Continue: [Experimental Status](experimental-status.md)
