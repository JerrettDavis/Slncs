# Experimental Status


Slncs is **not yet a stable product**. It is an exploratory project intended to test the ergonomics of defining .NET solution graphs in C#.

## What "Experimental" Means Here
| Area | Current State | Notes |
|------|---------------|-------|
| DSL Surface | Minimal / subject to change | Breaking changes possible pre-1.0. |
| `.slnx` Schema | Lean & intentionally narrow | May grow (metadata, attributes) with RFCs. |
| Performance | Acceptable for small/medium graphs | Large graphs: caching + parallelism TBD. |
| Tooling (`slncs-build`) | Dev-focused convenience | CLI options may expand / change. |
| IDE Awareness | Indirect via normal project load | No custom language services yet. |
| Security Model | Executes user C# script at build | Treat scripts as trusted code. |

## Stability Roadmap (Indicative)
| Milestone | Criteria |
|-----------|----------|
| 0.2.x | Feedback-driven refinements, doc completeness. |
| 0.3.x | Optional incremental caching, param injection helpers. |
| 0.4.x | Extended metadata (config groups, logical tags). |
| 0.5.x | RFC cycle for 1.0: freeze core schema + DSL verbs. |
| 1.0.0 | Backward compatibility guarantees begin. |

These are provisional and may change as community feedback evolves.

## Risks
- **Surface Creep**: Adding convenience verbs too early could lock in problematic abstractions.
- **Hidden Complexity**: Overusing C# features (reflection, dynamic discovery) could reduce clarity.
- **Ecosystem Expectations**: Users may expect full `.sln` parity prematurely.

## Mitigations
- Encourage extension methods in user land over core API expansion.
- Keep `.slnx` human-readable and diffable.
- RFC process before introducing breaking schema adjustments.

## Production Usage Guidance
| Scenario | Recommended? | Rationale |
|----------|--------------|----------|
| Internal tooling / prototypes | Yes | High learning value, low external coupling. |
| Mission critical build graph | Caution | Accept experimental risk & review script security. |
| OSS libraries | Maybe | Communicate experimental status to contributors. |
| Enterprise regulated environments | Not yet | Await stability + security hardening. |

## Opting Into Early Adoption
If you proceed in production-like settings:
1. Pin the `Slncs.Sdk` version in `global.json`.
2. Version-control the generated `.slnx` (optional) for audit OR document regeneration process.
3. Add a build audit step verifying script hash vs. expected (optional integrity check).

Continue: [FAQ](faq.md)
