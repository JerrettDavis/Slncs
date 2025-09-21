using Slncs.Sdk;

public class SlncsRunnerTests
{
    private static string RepoRoot()
    {
        var bin = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "..", ".."));
    }

    [Fact]
    public void Fails_When_Missing_Input()
    {
        var gen = Path.Combine(RepoRoot(), "SlncsGen", "bin", "Debug", "net8.0", "SlncsGen.dll");
        var exit = SlncsRunner.Run("does-not-exist.slncs.cs", Path.GetTempFileName(), gen);
        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void Generates_File_On_Valid_Input()
    {
        var root = RepoRoot();
        var tmp = Path.Combine(Path.GetTempPath(), "slncs-task-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var script = Path.Combine(tmp, "ok.slncs.cs");
        var outFile = Path.Combine(tmp, "ok.slnx");
        File.WriteAllText(script, """
                                  using Slncs;
                                  Solution.Create().Project(@"x\y.csproj").Write(OutputPath);
                                  """);

        var gen = Path.Combine(root, "SlncsGen", "bin", "Debug", "net8.0", "SlncsGen.dll");
        Assert.True(File.Exists(gen), "Build SlncsGen before running tests.");

        var exit = SlncsRunner.Run(script, outFile, gen);
        Assert.Equal(0, exit);
        Assert.True(File.Exists(outFile), "Runner should produce the .slnx file.");
    }
}