using System.Diagnostics;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace Slncs.Sdk.Tests;

public class SlncsRunnerTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static string RepoRoot()
    {
        var bin = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "..", ".."));
    }

    private static string FindSlncsGen()
    {
        var root = RepoRoot();
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(root, "src", "SlncsGen", "bin", cfg, "net8.0", "SlncsGen.dll");
            if (File.Exists(candidate)) return candidate;
        }

        return Path.Combine(root, "src", "SlncsGen", "bin", "Debug", "net8.0", "SlncsGen.dll");
    }

    private static ValueTask<string> GetSlncsGenPath()
    {

        var gen = FindSlncsGen();
        // If generator not built yet, build it quickly for the purpose of this test.
        if (!File.Exists(gen))
        {
            var root = RepoRoot();
            var proj = Path.Combine(root, "src", "SlncsGen", "SlncsGen.csproj");
            var procInfo = new ProcessStartInfo("dotnet", $"build \"{proj}\" -c Debug --nologo")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var p = Process.Start(procInfo)!;
            p.WaitForExit();
        }

        return new ValueTask<string>(gen);
    }

    private static ValueTask<int> RunSlncsGen(string script, string outFile)
        => new(SlncsRunner.Run(script, Path.GetTempFileName(), outFile));

    [Fact]
    public Task GivenBuildCommand_WhenSolutionDoesntExist_ThenReturnsNonZeroReturnCode()
        => Given("a build solution command", GetSlncsGenPath)
            .When("the solution file does not exist", s => RunSlncsGen("does-not-exist.slncs.cs", s))
            .Then("the return code should be non-zero", r => Assert.NotEqual(0, r))
            .AssertPassed();

    [Fact]
    public void Fails_When_Missing_Input()
    {
        var gen = FindSlncsGen();
        // If generator not built yet, build it quickly for the purpose of this test.
        if (!File.Exists(gen))
        {
            var root = RepoRoot();
            var proj = Path.Combine(root, "src", "SlncsGen", "SlncsGen.csproj");
            var procInfo = new ProcessStartInfo("dotnet", $"build \"{proj}\" -c Debug --nologo")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var p = Process.Start(procInfo)!;
            p.WaitForExit();
        }

        var exitCode = SlncsRunner.Run("does-not-exist.slncs.cs", Path.GetTempFileName(), gen);
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Generates_File_On_Valid_Input()
    {
        var root = RepoRoot();
        var tmp = Path.Combine(Path.GetTempPath(), "slncs-task-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var script = Path.Combine(tmp, "ok.slncs.cs");
        var outFile = Path.Combine(tmp, "ok.slnx");
        File.WriteAllText(
            script,
            """
            using Slncs;
            Solution.Create().Project(@"x/y.csproj").Write(OutputPath);
            """);

        var gen = FindSlncsGen();
        if (!File.Exists(gen))
        {
            // Attempt build if missing (CI Release builds, local Debug builds)
            var proj = Path.Combine(root, "src", "SlncsGen", "SlncsGen.csproj");
            var psi = new ProcessStartInfo("dotnet", $"build \"{proj}\" -c Release --nologo")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit();
        }

        Assert.True(File.Exists(gen), "SlncsGen.dll must be built before running tests (looked in Debug and Release).");

        var exit = SlncsRunner.Run(script, outFile, gen);
        Assert.Equal(0, exit);
        Assert.True(File.Exists(outFile), "Runner should produce the .slnx file.");
    }
}