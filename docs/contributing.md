# Contributing

Thank you for considering contributing to **Slncs**! Because this project is still experimental, early feedback and focused contributions are especially valuable.

## Ways to Contribute
| Type | Examples |
|------|----------|
| Feedback | Ergonomics, confusing behavior, missing docs |
| Bug reports | Crashes, incorrect `.slnx`, build forwarding issues |
| Feature proposals | Open an RFC issue with rationale + alternatives |
| Documentation | Clarify concepts, add examples, fix typos |
| Tests | Edge cases (paths, duplicates, unusual folder layouts) |

## Project Structure (High-Level)
| Path | Purpose |
|------|---------|
| `Slncs/` | Core DSL library (fluent solution builder) |
| `SlncsGen/` | Roslyn scripting host producing `.slnx` |
| `Slncs.Sdk/` | MSBuild tasks + SDK packaging (SlncsExec, SlnxParse) |
| `Slncs.Tool/` | `slncs-build` tool (pure script helper) |
| `samples/` | Example wrapper + script usage |
| `tests/` | Unit & end-to-end tests |
| `docs/` | DocFX site content |

## Development Workflow
```bash
# Clone
 git clone https://github.com/<you>/Slncs.git
 cd Slncs

# Build everything
dotnet build

# Run tests
dotnet test

# Try sample wrapper
dotnet build samples/template/MyCsSln.slncs

# Try pure script mode (create a single .slncs file)
dotnet run --project Slncs.Tool -- samples/pure/MyCsSln.slncs
```

## Branching & PRs
- Base all changes on `main` unless instructed otherwise.
- Keep PRs small & focused; large multi-theme PRs are harder to review.
- Include tests for new behavior or clearly justify why tests are not needed.
- Update or add documentation in `docs/` if user-visible behavior changes.

## Commit Message Guidelines ([Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/))
This project uses [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for commit messages.
Prefix your commit message with one of the following:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only changes
- `test`: Adding missing tests or correcting existing tests
- `refactor`: Code refactoring
- `perf`: Performance improvement
- `build`: Changes that affect the build system or external dependencies
- `ci`: Changes to our CI configuration files and scripts
- `chore`: Other changes that don't modify src or test files

Use the imperative, present tense: "change" not "changed" nor "changes".
Don't capitalize the first letter.

```
<type>[optional scope]: <description>
```

## RFC Process
For non-trivial features or schema additions:
1. Open an issue labeled `rfc`.
2. Provide: Problem, Motivation, Proposed Design, Alternatives, Risks.
3. Collaboratively refine; a maintainer will mark as accepted / needs revision / declined.
4. Submit implementation PR referencing the RFC issue.

## Testing Guidance
| Test Type | Location | Notes |
|-----------|----------|-------|
| DSL basics | `tests/Slncs.Tests/` | Generator smoke and DSL composition |
| Task behavior | `tests/Slncs.Sdk.Tests/` | SlncsExec & SlnxParse behavior |
| End-to-end | `tests/E2E.Tests/` | Full wrapper & pure script build pipelines |

Add tests where bugs could reappear (regressions). Deterministic ordering assertions are especially helpful.

## Coding Style
- Prefer explicit, readable code over cleverness in core tasks.
- Keep public surface documented (XML docs).
- Avoid unnecessary dependencies to keep package size minimal.

## Performance Considerations
If adding logic in generator or tasks:
- Avoid repeated disk scans.
- Defer expensive work until needed (lazy patterns welcome).

## Security Considerations
Scripts execute arbitrary C# during build:
- Do not add automatic network calls or side-effects without explicit user opt-in.
- Validate file existence gracefully (log & continue where safe).

## Reporting Security Issues
For any security-sensitive finding, open a GitHub security advisory or contact the maintainer privately—avoid filing public issues first.

## License
All contributions are under the project’s MIT license. By submitting a PR you agree to license your work accordingly.

## Recognition
We plan to maintain a CONTRIBUTORS section or acknowledgements after early stabilization. Your early feedback directly shapes the future direction.

---
*Thank you for helping explore a simpler way to express solution structure!*
