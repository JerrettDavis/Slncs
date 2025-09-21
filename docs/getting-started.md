# Getting Started


This guide walks through both supported workflows:
1. Wrapper project + C# script (`.slncs` + `.slncs.cs`)
2. Pure single-file C# script (`.slncs`) built with the `slncs-build` tool

---
## 1. Create a Wrapper Project (Optional Mode)
```
MySolution.slncs      (XML wrapper project)
MySolution.slncs.cs   (C# DSL script)
```
Wrapper file (`MySolution.slncs`):
```xml
<Project Sdk="Slncs.Sdk">
  <PropertyGroup>
    <SlncsFile>MySolution.slncs.cs</SlncsFile>
    <GeneratedSlnx>obj/MySolution.slnx</GeneratedSlnx>
  </PropertyGroup>
</Project>
```
Script (`MySolution.slncs.cs`):
```csharp
using Slncs;
Solution.Create()
  .Folder("/Solution Items", f => f.Files("Directory.Build.props"))
  .Project("src/Lib/Lib.csproj")
  .Project("src/App/App.csproj")
  .Write(OutputPath);
```
Build:
```bash
dotnet build MySolution.slncs
```
Result: `obj/MySolution.slnx` (+ `obj/MySolution.slnx.proj` aggregator).

---
## 2. Pure Script Mode (Tool-Assisted)
Single file `MySolution.slncs`:
```csharp
using Slncs;
Solution.Create()
  .Project("src/App/App.csproj")
  .Write(OutputPath);
```
Build with the tool:
```bash
slncs-build MySolution.slncs
```
The tool materializes a transient wrapper at `obj/.slncs-build/<Name>.wrapper.slncs` then performs the same pipeline as the wrapper mode.

---
## 3. Validate Output
Check for:

| Path | Purpose |
|------|---------|
| `obj/MySolution.slnx` | Generated solution manifest |
| `obj/MySolution.slnx.proj` | Aggregator (optional convenience) |
| `src/<Project>/bin/...` | Built assemblies from forwarded build |

---
## 4. Iterating
Edit the C# script and rebuild. Ordering & duplicates are normalized automatically.

---
## 5. Cleanup
Delete the wrapper (if using pure mode) or the generated `obj/` folder; nothing else is persisted.

Continue with: [DSL Reference](dsl-reference.md)
