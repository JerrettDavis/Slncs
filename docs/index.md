# Slncs Documentation

> Fluent C# DSL + experimental pipeline for authoring .NET solution graphs. **Status: Experimental / Early**

Welcome to the Slncs docs. This site has two major parts:
- **Conceptual Guides** (these markdown pages) – rationale, methodology, usage patterns.
- **API Reference** (generated from XML docs) – types like `SolutionBuilder`, `SlncsExec`, `SlnxParse`.

## Quick Links
- Getting Started: [Installation](installation.md) · [First Script](getting-started.md)
- DSL Reference: [API Essentials](dsl-reference.md)
- CI / Automation: [Tooling & CI](tooling-ci.md)
- Background: [Rationale](rationale.md) · [Methodology](methodology.md)
- Project Status: [Experimental Status](experimental-status.md) · [Roadmap](roadmap.md)
- Help: [FAQ](faq.md) · [Contributing](contributing.md)

## Overview
Slncs lets you define a solution using C# instead of a traditional `.sln` file. The script produces a deterministic `.slnx` manifest consumed by MSBuild tasks to build all referenced projects. Two workflows:

| Mode | Files | Invocation |
|------|-------|-----------|
| Wrapper | `My.slncs` (XML) + `My.slncs.cs` (C#) | `dotnet build My.slncs` |
| Pure Script | `My.slncs` (C# only) | `slncs-build My.slncs` |

Output artifacts (in `obj/`):
- `<Name>.slnx` – compact XML solution manifest.
- `<Name>.slnx.proj` – optional aggregator (NoTargets) referencing all projects.

## Why (In One Minute)
Traditional `.sln` files are noisy, imperative, and hard to compose. .NET's new XML-based `.slnx` format is a great step forward, but since developers are typically already working in .NET languages, why
not define the solution graph in C# directly? Slncs provides a fluent DSL unlocks programmability (loops, conditions, helpers) while keeping output minimal & deterministic for clean diffs.

## Core Differentiators
- Single language surface (C#) for graph & code.
- Deterministic ordering & de-duplication.
- Minimal intermediate format – easy to inspect & version.
- Optional aggregator for IDEs / omnibus builds.

## Try It (Pure Script Quickstart)
Create `Quick.slncs`:
```csharp
using Slncs;
Solution.Create()
  .Project("src/App/App.csproj")
  .Write(OutputPath);
```
Build:
```bash
slncs-build Quick.slncs
```
Inspect: `cat obj/Quick.slnx`.

## Experimental Disclaimer
Interfaces & schema may change prior to 1.0. Treat as an opt‑in ergonomic experiment; pin versions and review scripts like any build code.

Continue with [Installation](installation.md) or view the [DSL Reference](dsl-reference.md).
