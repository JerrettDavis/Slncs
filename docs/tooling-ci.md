# Tooling & CI


This page describes how to integrate Slncs into automated builds, code coverage, packaging, and local developer tooling.

## Build Pipelines
A typical sequence for **wrapper mode**:
```bash
dotnet restore
dotnet build MySolution.slncs -c Release -v:minimal
```
For **pure script mode**:
```bash
dotnet restore
slncs-build MySolution.slncs -- -c Release
```
(Any arguments after `--` are passed to the underlying `dotnet build` invocation in future extensions; current implementation forwards simple arguments directly.)

## CI Cache Considerations
- The transient wrapper for pure mode lives at `obj/.slncs-build/*.wrapper.slncs`; safe to cache `~/.nuget/packages` and build outputs normally.
- The `.slnx` is always regenerated; if optimization is needed, future RFC may add timestamp checks or hash caching.

## Code Coverage
Because the generator re-invokes `MSBuild` for each enumerated project, coverage tools that instrument at test invocation time (e.g., `dotnet test --collect:"XPlat Code Coverage"`) will work transparently.

## Packaging (NuGet)
If packaging libraries referenced by the generated graph, ensure the build that runs packaging occurs *after* the `.slnx` generation (normal if you build the wrapper or run `slncs-build` first). Standard `dotnet pack` flows are unaffected.

## Local Tool Workflow
1. Author or copy a `.slncs` script.
2. Run `slncs-build script.slncs`.
3. Open aggregator `obj/<Name>.slnx.proj` (optional) in IDE for unified load.

## IDE Integration
| IDE | Notes |
|-----|-------|
| Visual Studio | Open any constituent project or the aggregator. Wrapper project itself contains only targets, not source. |
| Rider | Open folder / aggregator. No special plugin required. |
| VS Code | Use C# extension; all referenced projects load via Omnisharp or Roslyn LSP. |

## MSBuild Customization
You can layer properties in the wrapper project file (wrapper mode) just like any SDK-style project – they will not affect nested projects unless forwarded deliberately. Example:
```xml
<PropertyGroup>
  <Deterministic>true</Deterministic>
  <Configuration Condition="'$(Configuration)' == ''">Debug</Configuration>
</PropertyGroup>
```

## Logging & Diagnostics
Set `-v:diag` to examine messages emitted by `SlncsExec` (`[slncs]` prefix) and `SlnxParse` (`[slncs-parse]`).

## Security Considerations
C# scripts are executed at build time via Roslyn scripting:
- Treat `.slncs` files like code (review, scanning, supply-chain policies).
- Avoid executing untrusted scripts.

## Reproducibility
Because ordering is normalized and duplicates are removed, the `.slnx` file should be stable given identical script content and project paths.

Continue: [Methodology](methodology.md)
