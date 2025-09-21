# Introduction

Slncs is an **experimental** approach to describing a .NET solution graph using *only C#* instead of a traditional `.sln` or `.slnx` file. You write a fluent DSL that enumerates projects, folders, and loose files; the build pipeline converts that script into a compact `.slnx` manifest and then builds the referenced projects.

Goals:
- Reduce cognitive switching between XML / specialized formats and source code.
- Provide programmability (conditions, loops, composition) for solution definition.
- Produce stable, deterministic artifacts suited to code review & diffing.

Non‑Goals (for now):
- Full parity with every Visual Studio `.sln`/`.slnx` feature (solution configs, nested GUID metadata, solution folders with GUID preservation, etc.).
- Replacing `.sln`/`.slnx` in all production contexts immediately.

Use cases explored:
- Rapid prototyping / scratch solution graphs.
- Generated or parameterized solutions (e.g., per tenant, per environment, feature subsets).
- Learning / experimentation on build graph ergonomics.

This section provides a conceptual overview. For concrete steps, see the *Getting Started* pages.
