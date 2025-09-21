# Rationale

Slncs explores whether **authoring solution graphs in C#** reduces friction versus the traditional `.sln` format.

## Motivation: Beyond the New Simplified Microsoft `.slnx`
Microsoft's newer **simplified XML solution format (`.slnx`)** (separate from this project) is a meaningful improvement over the legacy text `.sln`: it's cleaner, structured, and more tool-friendly. *However*, it still requires authors to hand-edit (or tooling to generate) a declarative file that lives outside the primary language used for the codebase.

**This project asks:** if we can already describe the graph declaratively in a structured XML form, *why not go one step further* and let developers express the same intent directly in **C#**, keeping all composition, conditional logic, and reuse inside the language + tooling ecosystem they already use hourly?

> We intentionally kept the output artifact name `.slnx` because the concept parallels "a simplified solution manifest"—but this project is **not** the official Microsoft `.slnx` implementation. If coexistence or ambiguity becomes a problem, we may consider emitting an alternate extension (e.g. `.cslnx`) via a future setting.

## Problems With Existing `.sln`
| Issue | Impact |
|-------|--------|
| Opaque text format | Hard for newcomers to safely edit by hand. |
| Merge churn | GUID ordering / sections cause noisy diffs & conflict risk. |
| Limited programmability | No native conditional logic, reuse, or composition. |
| Tool lock-in | `.sln` expresses more than most build automation needs. |
| Hard to parameterize | Generating variants (subset solutions, feature toggles) is manual. |

## Slncs Objectives
- Express the *intent* (project list, folders, accessory files) in a single language.
- Keep output deterministic and minimal for reviewing in PRs.
- Allow leveraging normal C# abstractions (methods, loops, constants) without inventing a new DSL parser.
- Decouple user-facing authoring from internal build representation (the `.slnx`).

## Why Not Patch `.sln` Instead?
The `.sln` format was not designed for structured extension, and its parsing/serialization nuances make robust programmatic manipulation brittle. A clean intermediate format plus high-level DSL avoids retrofitting constraints. Even the improved Microsoft `.slnx` still requires a distinct authoring surface.

## Payoffs to Validate
| Hypothesis | Metric / Signal |
|------------|-----------------|
| Faster onboarding | Fewer "what is this guid" questions in PRs. |
| Fewer merge conflicts | Reduced conflict frequency on solution graph changes. |
| Easier automation | Simpler generation of variant graphs (scripts, code-gen). |
| Lower context switching | Less time in separate file formats for mundane edits. |

## Non-Goals (Current Phase)
- Full fidelity of Visual Studio solution metadata.
- Storing solution configuration platforms or per-project nesting metadata.
- Guaranteeing backward compatibility before 1.0.

## Guiding Principle
> If you can describe it with a list + a bit of composition, you shouldn’t need an opaque format.

Continue to: [Methodology](methodology.md)
