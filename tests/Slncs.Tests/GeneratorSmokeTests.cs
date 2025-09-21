using System.Diagnostics;

namespace Slncs.Tests;

public class GeneratorSmokeTests
{
    private static string RepoRoot()
    {
        var bin = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "..", ".."));
    }

    [Fact]
    public void Generator_Produces_Slnx_From_Script()
    {
        var root = RepoRoot();
        var tmp = Path.Combine(Path.GetTempPath(), "slncs-gen-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var script = Path.Combine(tmp, "test.slncs.cs");
        var outPath = Path.Combine(tmp, "test.slnx");
        File.WriteAllText(script, """
                                  using Slncs;
                                  Solution.Create().Project(@"src\Demo\Demo.csproj").Write(OutputPath);
                                  """);

        var exe = Path.Combine(root, "src", "SlncsGen", "bin", "Debug", "net8.0", "SlncsGen.dll");
        Assert.True(File.Exists(exe), "SlncsGen.dll must be built before tests.");

        var psi = new ProcessStartInfo("dotnet", $"exec \"{exe}\" --slncs \"{script}\" --out \"{outPath}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = tmp
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();

        Assert.Equal(0, p.ExitCode);
        Assert.True(File.Exists(outPath), "Generator should emit the .slnx.");
    }
}