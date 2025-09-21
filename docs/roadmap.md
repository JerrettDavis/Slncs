# Roadmap


This roadmap is intentionally tentative—feedback can accelerate or prune items. Milestones below outline likely directions toward a future 1.0.

## Guiding Themes
- Keep core minimal until real usage pressures expansion.
- Preserve determinism & clarity over convenience early on.
- Enable opt‑in features with explicit user control.

## Near Term (0.2.x – 0.3.x)
| Item | Goal | Status |
|------|------|--------|
| Script hash caching (skip regeneration) | Faster no‑change builds | Planned |
| Parameter injection helpers | Cleaner env/config based graphs | Planned |
| Basic logging refinement | More concise default output | Planned |
| Additional sample scripts | Demonstrate patterns (monorepo, conditional) | In progress |

## Mid Term (0.4.x – 0.5.x)
| Item | Goal |
|------|------|
| Extended metadata (tags, groups) | Facilitate custom tooling / filtering |
| Optional solution configuration concept | Bridge gap with `.sln` scenarios |
| Rich CLI introspection (`list`, `graph`, `diff`) | Observability of solution shapes |
| Incremental aggregator optimization | Avoid rewriting unchanged aggregator |
| Editor services (path completion) | Authoring ergonomics |

## Pre‑1.0 Stabilization
| Item | Goal |
|------|------|
| Schema freeze proposal (RFC) | Lock in `.slnx` contract |
| Versioned schema header (optional) | Backward compatibility path |
| Formal security guidelines | Enterprise adoption posture |
| Performance benchmarks suite | Regressions detection |

## Post‑1.0 (Aspirational)
| Idea | Description |
|------|-------------|
| Multi‑language DSL adapters | F# / scripting wrappers generating same `.slnx` |
| Visual diff UI for `.slnx` | Friendly change inspection |
| Build graph visualizer | Dependency / grouping diagram generation |
| Hybrid mode with classic `.sln` | Fall back to `.sln` features when needed |

## RFC Process (Planned)
1. Open an issue tagged `rfc` with: Problem, Motivation, Proposed Design, Alternatives.
2. Discussion period (time‑boxed).
3. Draft spec PR (if accepted) updating docs & examples.
4. Implementation with gated feature flag (if breaking).

## How to Influence Roadmap
- Upvote existing issues / RFCs.
- Provide concrete scenarios (team size, solution size, pain points).
- Share performance traces if build overhead becomes material.

Continue: [Contributing](contributing.md)
