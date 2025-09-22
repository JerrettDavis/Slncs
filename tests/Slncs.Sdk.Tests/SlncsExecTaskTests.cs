using System.Diagnostics;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

// Added for [Feature]/[Scenario]

namespace Slncs.Sdk.Tests;

[Feature("Slncs Runner")]
public class SlncsRunnerTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // --------------------------- Helper / Environment ---------------------------
    private static string RepoRoot()
    {
        var bin = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "..", ".."));
    }

    private static string GeneratorCandidate(string cfg)
        => Path.Combine(RepoRoot(), "src", "SlncsGen", "bin", cfg, "net8.0", "SlncsGen.dll");

    private static string FindSlncsGen()
    {
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var candidate = GeneratorCandidate(cfg);
            if (File.Exists(candidate)) return candidate;
        }

        return GeneratorCandidate("Debug");
    }

    private static void BuildGeneratorIfMissing(string cfg)
    {
        var path = GeneratorCandidate(cfg);
        if (File.Exists(path)) return;
        var proj = Path.Combine(RepoRoot(), "src", "SlncsGen", "SlncsGen.csproj");
        var psi = new ProcessStartInfo("dotnet", $"build \"{proj}\" -c {cfg} --nologo")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
    }

    // Ensures generator is built (tries Release then Debug) and returns its path
    private static string EnsureGeneratorBuilt()
    {
        BuildGeneratorIfMissing("Release");
        BuildGeneratorIfMissing("Debug");
        return FindSlncsGen();
    }

    private static ValueTask<int> RunSlncsGen(string script, string genPath)
        => new(SlncsRunner.Run(script, Path.GetTempFileName(), genPath));

    private static ValueTask<int> RunMissingSolution(string genPath)
        => RunSlncsGen("does-not-exist.slncs.cs", genPath);

    // --------------------------- Step Methods (Assertions) ---------------------------
    private static void AssertGeneratorPathNotNull(string genPath) => Assert.False(string.IsNullOrWhiteSpace(genPath));

    private static void AssertGeneratorFileExists(string genPath)
        => Assert.True(File.Exists(genPath), "SlncsGen.dll must exist (built in Debug or Release).");

    private static void AssertNonZeroExit(int exitCode) => Assert.NotEqual(0, exitCode);
    private static void AssertPositiveExit(int exitCode) => Assert.True(exitCode > 0, "Exit code should be > 0 on failure.");
    private static void AssertZeroExit(int exitCode) => Assert.Equal(0, exitCode);

    private static void AssertOutputFileExists((int ExitCode, string OutFile) result)
        => Assert.True(File.Exists(result.OutFile), "Output .slnx file should be created.");

    private static void AssertOutputFileNotEmpty((int ExitCode, string OutFile) result)
    {
        var len = new FileInfo(result.OutFile).Length;
        Assert.True(len > 0, "Output file should not be empty.");
    }

    // --------------------------- Scenario 1 ---------------------------
    [Fact]
    [Scenario("Given a build command when the solution file is missing then a non-zero exit code should be returned")]
    public Task Missing_Solution_Returns_NonZero_Code()
        => Given("an ensured generator build", EnsureGeneratorBuilt)
            .And("a valid generator path string", AssertGeneratorPathNotNull)
            .And("the generator file exists", AssertGeneratorFileExists)
            .When("executing the build against a missing script", RunMissingSolution)
            .Then("a non-zero exit code is returned", AssertNonZeroExit)
            .And("the exit code is positive", AssertPositiveExit)
            .AssertPassed();

    // --------------------------- Scenario 2 ---------------------------
    // Expanded surface area around ensuring build + re-running missing script path.
    [Fact]
    [Scenario("Given a generator that has been built when run with an invalid script then it fails appropriately")]
    public Task Missing_Input_Failure_Flow()
        => Given("a generator built in Release or Debug", EnsureGeneratorBuilt)
            .Then("the generator file should exist", AssertGeneratorFileExists)
            .When("running with a non-existent .slncs.cs script", RunMissingSolution)
            .Then("the process indicates failure via non-zero exit code", AssertNonZeroExit)
            .And("the non-zero exit code is > 0", AssertPositiveExit)
            .AssertPassed();

    // --------------------------- Scenario 3 ---------------------------
    private static (string Script, string OutFile) CreateValidScript(string genPath)
    {
        // genPath unused directly here, but keeps signature uniform for chain context.
        var tmp = Path.Combine(Path.GetTempPath(), "slncs-task-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);
        var script = Path.Combine(tmp, "ok.slncs.cs");
        var outFile = Path.Combine(tmp, "ok.slnx");
        File.WriteAllText(script, """
                                  using Slncs;
                                  Solution.Create().Project(@"x/y.csproj").Write(OutputPath);
                                  """);
        return (script, outFile);
    }

    private static (int ExitCode, string OutFile) RunValidScript((string Script, string OutFile) pair)
    {
        var gen = FindSlncsGen();
        Assert.True(File.Exists(gen), "Generator must exist before executing valid script scenario.");
        var exit = SlncsRunner.Run(pair.Script, pair.OutFile, gen);
        return (exit, pair.OutFile);
    }

    [Fact]
    [Scenario("Given a valid solution script when executed then an output slnx file is produced successfully")]
    public Task Generates_File_On_Valid_Input()
        => Given("an ensured generator build", EnsureGeneratorBuilt)
            .Then("generator should exist for execution", AssertGeneratorFileExists)
            .When("creating a valid solution generation script", CreateValidScript)
            .When("executing the script with the generator", RunValidScript)
            .Then("exit code is zero", r => AssertZeroExit(r.ExitCode))
            .And("output file exists", AssertOutputFileExists)
            .And("output file is not empty", AssertOutputFileNotEmpty)
            .AssertPassed();
}