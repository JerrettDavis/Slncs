using System.Diagnostics;

namespace Slncs.Tests;

public class GeneratorSmokeTests
{
    private static string RepoRoot()
    {
        var bin = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "..", ".."));
    }

    private static string DetectConfiguration()
    {
        var bin = AppContext.BaseDirectory;
        return bin.IndexOf(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0 ? "Release" : "Debug";
    }

    [Fact]
    public void Generator_Produces_Slnx_From_Script()
    {
        var root = RepoRoot();
        var cfg = DetectConfiguration();
        var tmp = Path.Combine(Path.GetTempPath(), "slncs-gen-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var script = Path.Combine(tmp, "test.slncs.cs");
        var outPath = Path.Combine(tmp, "test.slnx");
        File.WriteAllText(script, """
                                  using Slncs;
                                  Solution.Create().Project(@"src\Demo\Demo.csproj").Write(OutputPath);
                                  """);

        var exe = Path.Combine(root, "src", "SlncsGen", "bin", cfg, "net8.0", "SlncsGen.dll");
        if (!File.Exists(exe))
        {
            // Attempt to build the generator for current configuration (especially needed in CI Release + --no-build scenarios)
            var proj = Path.Combine(root, "src", "SlncsGen", "SlncsGen.csproj");
            var build = Process.Start(new ProcessStartInfo("dotnet", $"build \"{proj}\" -c {cfg} --nologo")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = root
            });
            build!.WaitForExit();
        }

        Assert.True(File.Exists(exe), $"SlncsGen.dll must be built before tests (looked for {exe}).");

        var psi = new ProcessStartInfo("dotnet", $"exec \"{exe}\" --slncs \"{script}\" --out \"{outPath}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = tmp
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();

        if (p.ExitCode != 0)
        {
            throw new Xunit.Sdk.XunitException($"Generator exited with code {p.ExitCode}\nSTDOUT:\n{File.ReadAllText(script)}");
        }

        Assert.Equal(0, p.ExitCode);
        Assert.True(File.Exists(outPath), "Generator should emit the .slnx.");
    }
}