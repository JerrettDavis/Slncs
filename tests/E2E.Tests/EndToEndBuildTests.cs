using System.Diagnostics;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace E2E.Tests;

[Feature("End To End Build")]
public class EndToEndBuildTests : TinyBddXunitBase, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _feedDir;
    private readonly string _config;
    private readonly string _nugetConfigPath;
    private readonly string _generatorDllPath; // local built generator dll
    private readonly string _sdkVersionUsed;   // base version packed into local feed
    private readonly string _globalJsonPath;

    private static string PackLockPath => Path.Combine(RepoRoot(), ".e2e-pack.lock");

    public EndToEndBuildTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
        _config = DetectConfiguration();
        _feedDir = Path.Combine(RepoRoot(), "samples", ".e2e-local-feed");
        Directory.CreateDirectory(_feedDir);

        var baseVersion = ReadBaseSdkVersion();
        _sdkVersionUsed = baseVersion;
        EnsureSdkPacked(baseVersion);

        _generatorDllPath = Path.Combine(RepoRoot(), "src", "SlncsGen", "bin", _config, "net8.0", "SlncsGen.dll");
        if (!File.Exists(_generatorDllPath)) BuildLocalGenerator();
        if (!File.Exists(_generatorDllPath)) throw new FileNotFoundException("Failed to build local SlncsGen.dll for E2E tests", _generatorDllPath);
        _output.WriteLine($"[e2e] Using local generator dll: {_generatorDllPath}");
        _output.WriteLine($"[e2e] Using SDK version: {_sdkVersionUsed}");

        var samplesRoot = Path.Combine(RepoRoot(), "samples");
        _nugetConfigPath = Path.Combine(samplesRoot, "nuget.config");
        var nugetConfig =
            $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <packageSources>\n    <clear />\n    <add key=\"local-e2e\" value=\"{_feedDir}\" />\n    <add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" />\n  </packageSources>\n</configuration>";
        File.WriteAllText(_nugetConfigPath, nugetConfig);
        try
        {
            var pureDir = Path.Combine(samplesRoot, "pure");
            var templateDir = Path.Combine(samplesRoot, "template");
            File.Copy(_nugetConfigPath, Path.Combine(pureDir, "nuget.config"), true);
            File.Copy(_nugetConfigPath, Path.Combine(templateDir, "nuget.config"), true);
        }
        catch { /* ignored */ }
        // Write global.json to pin msbuild SDK version
        _globalJsonPath = Path.Combine(samplesRoot, "global.json");
        var globalJson = $"{{\n  \"msbuild-sdks\": {{ \n    \"Slncs.Sdk\": \"{_sdkVersionUsed}\" \n  }}\n}}";
        File.WriteAllText(_globalJsonPath, globalJson);
        // Purge stale versioned wrapper artifacts from prior test runs (placed incorrectly under obj)
        TryDeletePattern(Path.Combine(samplesRoot, "template", "obj"), ".e2e-versioned-*.slncs");
        TryDeletePattern(Path.Combine(samplesRoot, "pure", "obj"), ".e2e-versioned-*.slncs");
    }

    private record E2EContext(
        string Config,
        string FeedDir,
        string GeneratorDll,
        string SamplesBaseDir,
        string TemplateDir,
        string PureDir,
        string WrapperPath,
        string? PureScriptPath,
        string? OutSlnxPath,
        int ExitCode,
        string StdOut,
        string StdErr
    );

    private E2EContext CreateBaseContext()
    {
        var root = RepoRoot();
        var samplesBaseDir = Path.Combine(root, "samples");
        var templateDir = Path.Combine(samplesBaseDir, "template");
        var pureDir = Path.Combine(samplesBaseDir, "pure");
        var wrapper = Path.Combine(templateDir, "MyCsSln.slncs");
        Assert.True(File.Exists(wrapper), "Sample wrapper must exist.");
        return new E2EContext(_config, _feedDir, _generatorDllPath, samplesBaseDir, templateDir, pureDir, wrapper, null, null, 0, string.Empty,
            string.Empty);
    }

    private E2EContext BuildWrapper(E2EContext ctx)
    {
        // Clean any stale temp versioned wrappers from prior runs
        var objDir = Path.Combine(ctx.TemplateDir, "obj");
        if (Directory.Exists(objDir))
        {
            foreach (var f in Directory.EnumerateFiles(objDir, ".e2e-versioned-*.slncs", SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(f); } catch { /* ignore */ }
            }
        }
        var psi = new ProcessStartInfo("dotnet", $"build \"{ctx.WrapperPath}\" -c {ctx.Config} -v:m --nologo")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = ctx.TemplateDir,
            Environment = { ["SLNCS_GEN_DLL"] = ctx.GeneratorDll }
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        return ctx with { ExitCode = p.ExitCode, StdOut = stdout, StdErr = stderr, OutSlnxPath = Path.Combine(ctx.TemplateDir, "obj", "MyCsSln.slnx") };
    }

    private void AssertWrapperBuildSucceeded(E2EContext ctx)
    {
        if (ctx.ExitCode != 0)
        {
            _output.WriteLine("--- wrapper stdout ---\n" + ctx.StdOut);
            _output.WriteLine("--- wrapper stderr ---\n" + ctx.StdErr);
        }
        Assert.Equal(0, ctx.ExitCode);
    }

    private void AssertWrapperArtifacts(E2EContext ctx)
    {
        Assert.True(ctx.OutSlnxPath is not null && File.Exists(ctx.OutSlnxPath), ".slnx should be generated by wrapper.");
        var consoleDll = Path.Combine(ctx.TemplateDir, "src", "ConsoleApp1", "bin", ctx.Config, "net8.0", "ConsoleApp1.dll");
        if (!File.Exists(consoleDll))
        {
            _output.WriteLine("ConsoleApp1.dll missing; dumping directory tree under samples/template/src:");
            foreach (var path in Directory.EnumerateFiles(Path.Combine(ctx.TemplateDir, "src"), "*", SearchOption.AllDirectories))
                _output.WriteLine(path);
        }
        Assert.True(File.Exists(consoleDll), "Console application should have been built by direct parse pipeline.");
    }

    private E2EContext CreatePureScript(E2EContext ctx)
    {
        var scriptPath = Path.Combine(ctx.PureDir, "MyCsSlnSingle.slncs");
        File.WriteAllText(scriptPath,
            "using Slncs;\nSolution.Create()\n    .Folder(\"/Solution Items\", f => f.Files(\"Directory.Build.props\"))\n    .Project(@\"../template/src/ClassLibrary1/ClassLibrary1.csproj\")\n    .Project(@\"../template/src/ConsoleApp1/ConsoleApp1.csproj\")\n    .Write(OutputPath);\n");
        return ctx with { PureScriptPath = scriptPath };
    }

    private E2EContext RunTool(E2EContext ctx)
    {
        Assert.NotNull(ctx.PureScriptPath);
        var toolProj = Path.Combine(RepoRoot(), "src", "Slncs.Tool", "Slncs.Tool.csproj");
        using (FileLock.Acquire(PackLockPath + ".tool-build", TimeSpan.FromSeconds(60), _output))
        {
            var buildDll = Path.Combine(RepoRoot(), "src", "Slncs.Tool", "bin", ctx.Config, "net8.0", "Slncs.Tool.dll");
            if (!File.Exists(buildDll))
            {
                var buildPsi = new ProcessStartInfo("dotnet", $"build \"{toolProj}\" -c {ctx.Config} -f net8.0 --nologo")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    WorkingDirectory = RepoRoot()
                };
                using var bp = Process.Start(buildPsi)!;
                bp.WaitForExit();
                if (bp.ExitCode != 0 && !File.Exists(buildDll))
                {
                    var bStdOut = bp.StandardOutput.ReadToEnd();
                    var bStdErr = bp.StandardError.ReadToEnd();
                    _output.WriteLine("Failed to build Slncs.Tool. StdOut:\n" + bStdOut);
                    _output.WriteLine("StdErr:\n" + bStdErr);
                    return ctx with { ExitCode = bp.ExitCode, StdOut = bStdOut, StdErr = bStdErr };
                }
            }
        }
        var psi = new ProcessStartInfo("dotnet", $"run --framework net8.0 --project \"{toolProj}\" -c {ctx.Config} --no-build -- \"{ctx.PureScriptPath}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(ctx.PureScriptPath)!,
            Environment = { ["SLNCS_GEN_DLL"] = ctx.GeneratorDll }
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        var baseName = Path.GetFileNameWithoutExtension(ctx.PureScriptPath);
        return ctx with { ExitCode = p.ExitCode, StdOut = stdout, StdErr = stderr, OutSlnxPath = Path.Combine(ctx.PureDir, "obj", baseName + ".slnx") };
    }

    private void AssertToolSucceeded(E2EContext ctx)
    {
        if (ctx.ExitCode != 0)
        {
            _output.WriteLine("--- tool stdout ---\n" + ctx.StdOut);
            _output.WriteLine("--- tool stderr ---\n" + ctx.StdErr);
        }
        Assert.Equal(0, ctx.ExitCode);
    }

    private void AssertToolArtifacts(E2EContext ctx)
    {
        Assert.True(ctx.OutSlnxPath is not null && File.Exists(ctx.OutSlnxPath), ".slnx should be generated for pure script.");
        var consoleDll = Path.Combine(ctx.SamplesBaseDir, "template", "src", "ConsoleApp1", "bin", ctx.Config, "net8.0", "ConsoleApp1.dll");
        Assert.True(File.Exists(consoleDll), "Console application should have been built by slncs-build tool.");
    }

    [Fact]
    [Scenario("Given an initialized context when building the wrapper script then expected projects are built")]
    public Task Dotnet_Build_Works_On_Slncs_Wrapper()
        => Given("an initialized e2e context", CreateBaseContext)
            .When("the wrapper project is built", BuildWrapper)
            .Then("the wrapper build succeeds", AssertWrapperBuildSucceeded)
            .And("wrapper artifacts exist", AssertWrapperArtifacts)
            .AssertPassed();

    [Fact]
    [Scenario("Given an initialized context when running the slncs build tool on a pure script then an slnx and built outputs result")]
    public Task Slncs_Build_Tool_Works_On_Pure_Script()
        => Given("an initialized e2e context", CreateBaseContext)
            .When("a pure script is created", CreatePureScript)
            .When("the tool is executed", RunTool)
            .Then("the tool run succeeds", AssertToolSucceeded)
            .And("tool artifacts exist", AssertToolArtifacts)
            .AssertPassed();

    private void BuildLocalGenerator()
    {
        var root = RepoRoot();
        var genProj = Path.Combine(root, "src", "SlncsGen", "SlncsGen.csproj");
        var psi = new ProcessStartInfo("dotnet", $"build \"{genProj}\" -c {_config} --nologo")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = root
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode == 0) return;
        _output.WriteLine("Failed to build SlncsGen. StdOut:\n" + p.StandardOutput.ReadToEnd());
        _output.WriteLine("StdErr:\n" + p.StandardError.ReadToEnd());
    }

    private static string DetectConfiguration()
    {
        var bin = AppContext.BaseDirectory;
        return bin.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
    }

    private void EnsureSdkPacked(string version)
    {
        var nupkg = Path.Combine(_feedDir, $"Slncs.Sdk.{version}.nupkg");
        if (File.Exists(nupkg)) return; // already packed by earlier TFM
        using var packLock = FileLock.Acquire(PackLockPath, TimeSpan.FromSeconds(60), _output);
        if (File.Exists(nupkg)) return; // double-checked
        var root = RepoRoot();
        var sdkProj = Path.Combine(root, "src", "Slncs.Sdk", "Slncs.Sdk.csproj");
        var psi = new ProcessStartInfo("dotnet", $"pack \"{sdkProj}\" -c {_config} -o \"{_feedDir}\" --nologo")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0 || !File.Exists(nupkg))
        {
            _output.WriteLine("Failed to pack Slncs.Sdk. StdOut:\n" + p.StandardOutput.ReadToEnd());
            _output.WriteLine("StdErr:\n" + p.StandardError.ReadToEnd());
            throw new Exception("Failed to pack Slncs.Sdk for local feed");
        }
    }

    private static string RepoRoot()
    {
        var bin = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "..", ".."));
    }

    private static string ReadBaseSdkVersion()
    {
        try
        {
            var root = RepoRoot();
            var csproj = Path.Combine(root, "src", "Slncs.Sdk", "Slncs.Sdk.csproj");
            var text = File.ReadAllText(csproj);
            var start = text.IndexOf("<Version>", StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                var end = text.IndexOf("</Version>", start, StringComparison.OrdinalIgnoreCase);
                if (end > start)
                {
                    var val = text.Substring(start + 9, end - (start + 9)).Trim();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
        }
        catch { }
        return "0.1.0";
    }

    public new void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (!string.IsNullOrEmpty(_nugetConfigPath) && File.Exists(_nugetConfigPath)) File.Delete(_nugetConfigPath); } catch { }
        try { if (!string.IsNullOrEmpty(_globalJsonPath) && File.Exists(_globalJsonPath)) File.Delete(_globalJsonPath); } catch { }
    }

    private static void TryDeletePattern(string dir, string pattern)
    {
        if (!Directory.Exists(dir)) return;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(f); } catch { /* ignore individual */ }
            }
        }
        catch { /* ignore */ }
    }

    private sealed class FileLock : IDisposable
    {
        private readonly FileStream _stream;
        private FileLock(FileStream stream) => _stream = stream;
        public static FileLock Acquire(string path, TimeSpan timeout, ITestOutputHelper output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var sw = Stopwatch.StartNew();
            while (true)
            {
                try { return new FileLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
                catch (IOException)
                {
                    if (sw.Elapsed > timeout) throw new TimeoutException($"Timed out acquiring file lock: {path}");
                    Thread.Sleep(150);
                }
                catch (UnauthorizedAccessException)
                {
                    if (sw.Elapsed > timeout) throw new TimeoutException($"Timed out acquiring file lock (unauthorized): {path}");
                    Thread.Sleep(150);
                }
            }
        }
        public void Dispose() => _stream.Dispose();
    }
}
