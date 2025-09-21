# DSL Reference


The Slncs DSL exposes a minimal, fluent surface centered on `Solution.Create()` and chained builder calls.

## Entry Point
```csharp
using Slncs;
var sol = Solution.Create();
```

## Adding Projects
```csharp
sol.Project("src/Lib/Lib.csproj")
   .Project("src/App/App.csproj");
```
- Paths are treated as relative (recommended). Absolute paths are allowed but reduce portability.
- Duplicate paths (case-insensitive on Windows) are coalesced.

## Adding Folders & Files
```csharp
sol.Folder("/Solution Items", f =>
{
    f.Files("README.md", "Directory.Build.props")
     .Folder("docs", d => d.File("architecture.md"));
});
```
Rules:
- Folder names are normalized to end with `/` when serialized.
- Files are included verbatim; no existence check at generation time (MSBuild build phase will surface missing files if relevant).

## Chaining & Immutability
The builder mutates internal collections for brevity; it returns `this` to support chaining. A single terminal call to `Write(OutputPath)` is standard (where `OutputPath` is supplied by the scripting host).

## Writing Output
```csharp
sol.Write(OutputPath); // Appends .slnx if absent
```
`OutputPath` should point to an `.slnx` path (the generator passes a path inside `obj/`).

## Advanced Patterns
### Conditional Inclusion
```csharp
var includeSamples = Environment.GetEnvironmentVariable("INCLUDE_SAMPLES") == "1";
if (includeSamples)
    sol.Project("samples/SampleApp/SampleApp.csproj");
```

### Helper Methods for Reuse
```csharp
static void Backend(SolutionBuilder b)
{
    b.Project("src/Core/Core.csproj")
     .Project("src/Api/Api.csproj");
}

Solution.Create()
    .Folder("/Solution Items", f => f.File("Directory.Build.props"))
    .Also(Backend) // extension method pattern (see below)
    .Write(OutputPath);
```
Possible extension:
```csharp
public static class SolutionBuilderExtensions
{
    public static SolutionBuilder Also(this SolutionBuilder b, Action<SolutionBuilder> compose)
    { compose(b); return b; }
}
```

### Dynamic Discovery (Experimental Idea)
Enumerate projects by pattern (manual implementation):
```csharp
foreach (var csproj in Directory.GetFiles("src", "*.csproj", SearchOption.AllDirectories))
    sol.Project(csproj);
```
Be cautious: ordering is canonicalized, but discovery cost should be kept low.

## Output Schema (`.slnx`)
```xml
<Solution>
  <Folder Name="/Solution Items/">
    <File Path="README.md" />
  </Folder>
  <Project Path="src/Lib/Lib.csproj" />
  <Project Path="src/App/App.csproj" />
</Solution>
```
No GUIDs, no configurations, no nested solution folder metadata; intentionally lean.

## Aggregator Project
Generated as `<Output>.slnx.proj` (NoTargets). Example:
```xml
<Project Sdk="Microsoft.Build.NoTargets/3.6.0">
  <ItemGroup>
    <ProjectReference Include="src/Lib/Lib.csproj" />
    <ProjectReference Include="src/App/App.csproj" />
  </ItemGroup>
</Project>
```

## Deterministic Ordering
Sort precedence: Folders (0), Projects (1), Files (2); then ordinal string ordering. This ensures stable diffs.

## Limitations
| Area | Status |
|------|--------|
| Solution configurations | Not supported |
| Per-project solution nesting GUIDs | Not stored |
| Custom solution-level build events | Out of scope |
| Cross-language project metadata | Future RFC |

Continue: [Tooling & CI](tooling-ci.md)
