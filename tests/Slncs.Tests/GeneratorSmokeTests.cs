using System.Diagnostics;
using System.Xml.Linq;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace Slncs.Tests;

[Feature("Generator Smoke")]
public class GeneratorSmokeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ---------------- Helpers ----------------
    private static string RepoRoot()
    {
        var bin = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "..", ".."));
    }

    private static string DetectConfiguration()
    {
        var bin = AppContext.BaseDirectory;
        return bin.IndexOf(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
            ? "Release"
            : "Debug";
    }

    private static string EnsureGeneratorPath()
    {
        var root = RepoRoot();
        var cfg = DetectConfiguration();
        var exe = Path.Combine(root, "src", "SlncsGen", "bin", cfg, "net8.0", "SlncsGen.dll");
        if (File.Exists(exe))
            return exe;
        var proj = Path.Combine(root, "src", "SlncsGen", "SlncsGen.csproj");
        var psi = new ProcessStartInfo("dotnet", $"build \"{proj}\" -c {cfg} --nologo")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        return exe;
    }

    private record GenContext(string Generator, string Script, string OutPath, int ExitCode);

    private static GenContext InitContext(string generatorPath) => new(generatorPath, string.Empty, string.Empty, 0);

    private static GenContext CreateValidScript(GenContext ctx)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "slncs-gen-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var script = Path.Combine(tmp, "test.slncs.cs");
        var outPath = Path.Combine(tmp, "test.slnx");
        File.WriteAllText(script, """
                                  using Slncs;
                                  Solution.Create().Project(@"src\\Demo\\Demo.csproj").Write(OutputPath);
                                  """);
        return ctx with { Script = script, OutPath = outPath };
    }

    private static GenContext ExecuteGenerator(GenContext ctx)
    {
        Assert.True(File.Exists(ctx.Generator), $"SlncsGen.dll must be built before tests (looked for {ctx.Generator}).");
        var psi = new ProcessStartInfo("dotnet", $"exec \"{ctx.Generator}\" --slncs \"{ctx.Script}\" --out \"{ctx.OutPath}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(ctx.Script)!
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        return ctx with { ExitCode = p.ExitCode };
    }

    // ---------------- Assertions ----------------
    private static void AssertGeneratorExists(string exe) => Assert.True(File.Exists(exe), $"Generator not found at {exe}");
    private static void AssertExitCodeZero(GenContext ctx) => Assert.Equal(0, ctx.ExitCode);
    private static void AssertOutputExists(GenContext ctx) => Assert.True(File.Exists(ctx.OutPath), "Generator should emit the .slnx.");
    private static void AssertOutputNonEmpty(GenContext ctx) => Assert.True(new FileInfo(ctx.OutPath).Length > 0, "Output file should not be empty.");

    private static void AssertOutputXmlHasSolutionRoot(GenContext ctx)
    {
        var doc = XDocument.Load(ctx.OutPath);
        Assert.NotNull(doc.Root);
        Assert.Equal("Solution", doc.Root!.Name.LocalName);
    }

    // ---------------- Scenario ----------------
    [Fact]
    [Scenario("Given a generator when a valid solution script is executed then an slnx file is produced successfully")]
    public Task Generator_Produces_Slnx_From_Script()
        => Given("an ensured generator path", EnsureGeneratorPath)
            .Then("the generator exists", AssertGeneratorExists)
            .When("a generation context is initialized", InitContext)
            .When("a valid solution script is created", CreateValidScript)
            .When("the generator is executed", ExecuteGenerator)
            .Then("the exit code is zero", AssertExitCodeZero)
            .And("the output file exists", AssertOutputExists)
            .And("the output file is not empty", AssertOutputNonEmpty)
            .And("the output XML has a Solution root", AssertOutputXmlHasSolutionRoot)
            .AssertPassed();
}