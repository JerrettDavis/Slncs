using System.Diagnostics;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace E2E.Tests;

[Feature("End To End Build")] // Added feature annotation
public class EndToEndBuildTests : TinyBddXunitBase, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _feedDir;
    private readonly string _config;
    private readonly string _nugetConfigPath;
    private readonly string _tempGlobalJsonPath;
    private readonly string _generatorDllPath; // local built generator dll
    private readonly string _sdkVersionUsed;   // version packed into local feed

    public EndToEndBuildTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
        _config = DetectConfiguration();
        _feedDir = Path.Combine(RepoRoot(), "samples", ".e2e-local-feed");
        // Create local feed
        if (Directory.Exists(_feedDir))
        {
            try
            {
                Directory.Delete(_feedDir, true);
            }
            catch
            {
                /* ignored */
            }
        }

        Directory.CreateDirectory(_feedDir);

        var baseVersion = ReadBaseSdkVersion();
        _sdkVersionUsed = baseVersion + "-e2e-" + DateTime.UtcNow.Ticks;
        PackLocalSdk(_sdkVersionUsed);

        // Compute local generator dll path (from source tree) and ensure it exists (build if necessary)
        _generatorDllPath = Path.Combine(RepoRoot(), "src", "SlncsGen", "bin", _config, "net8.0", "SlncsGen.dll");
        if (!File.Exists(_generatorDllPath)) BuildLocalGenerator();
        if (!File.Exists(_generatorDllPath)) throw new FileNotFoundException("Failed to build local SlncsGen.dll for E2E tests", _generatorDllPath);
        _output.WriteLine($"[e2e] Using local generator dll: {_generatorDllPath}");

        // Write transient nuget.config at samples root
        var samplesRoot = Path.Combine(RepoRoot(), "samples");
        _nugetConfigPath = Path.Combine(samplesRoot, "nuget.config");
        var nugetConfig =
            $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <packageSources>\n    <clear />\n    <add key=\"local-e2e\" value=\"{_feedDir}\" />\n    <add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" />\n  </packageSources>\n</configuration>";
        File.WriteAllText(_nugetConfigPath, nugetConfig);
        // Write temporary global.json to samples root overriding msbuild-sdks mapping for Slncs.Sdk
        _tempGlobalJsonPath = Path.Combine(samplesRoot, "global.json");
        var tmpGlobal = $"{{\n  \"msbuild-sdks\": {{\n    \"Slncs.Sdk\": \"{_sdkVersionUsed}\"\n  }}\n}}";
        File.WriteAllText(_tempGlobalJsonPath, tmpGlobal);
        _output.WriteLine($"[e2e] Using temporary SDK version: {_sdkVersionUsed}");
    }

    // ---------------- Context ----------------
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
        string StdErr,
        string SdkVersion
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
            string.Empty, _sdkVersionUsed);
    }

    // ---------------- Wrapper Scenario Steps ----------------
    private E2EContext BuildWrapper(E2EContext ctx)
    {
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
        return ctx with
        {
            ExitCode = p.ExitCode,
            StdOut = stdout,
            StdErr = stderr,
            OutSlnxPath = Path.Combine(ctx.TemplateDir, "obj", "MyCsSln.slnx")
        };
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

    // ---------------- Tool Scenario Steps ----------------
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
        var toolDir = Path.Combine(RepoRoot(), "src", "Slncs.Tool");
        Assert.True(Directory.Exists(toolDir), "Tool project must exist");
        var psi = new ProcessStartInfo("dotnet", $"run --framework net8.0 --project \"{toolDir}\" -c {ctx.Config} -- \"{ctx.PureScriptPath}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = ctx.PureDir,
            Environment = { ["SLNCS_GEN_DLL"] = ctx.GeneratorDll }
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        return ctx with
        {
            ExitCode = p.ExitCode,
            StdOut = stdout,
            StdErr = stderr,
            OutSlnxPath = Path.Combine(ctx.PureDir, "obj", "MyCsSlnSingle.slnx")
        };
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

    private ValueTask CleanupPureScript(E2EContext ctx)
    {
        if (ctx.PureScriptPath is not { } p || !File.Exists(p))
            return new ValueTask();

        try
        {
            File.Delete(p);
        }
        catch
        {
            /* ignored */
        }

        return new ValueTask();
    }

    // ---------------- Scenarios ----------------
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
            .And("cleanup pure script", CleanupPureScript)
            .AssertPassed();

    // ---------------- Existing Helper Methods (unchanged) ----------------
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
        return bin.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
    }

    private void PackLocalSdk(string versionOverride)
    {
        var root = RepoRoot();
        var sdkProj = Path.Combine(root, "src", "Slncs.Sdk", "Slncs.Sdk.csproj");
        var psi = new ProcessStartInfo("dotnet", $"pack \"{sdkProj}\" -c {_config} -o \"{_feedDir}\" /p:Version={versionOverride} --nologo")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            _output.WriteLine("Failed to pack Slncs.Sdk (override). StdOut:\n" + p.StandardOutput.ReadToEnd());
            _output.WriteLine("StdErr:\n" + p.StandardError.ReadToEnd());
            throw new Exception("Failed to pack overridden Slncs.Sdk for local feed");
        }
    }

    private static string RepoRoot()
    {
        var bin = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(bin, "..", "..", "..", "..", ".."));
    }

    private static string ReadBaseSdkVersion()
    {
        // Fallback to 0.1.0 if not found
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
        catch
        {
            /* ignored */
        }

        return "0.1.0";
    }

    public new void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (Directory.Exists(_feedDir)) Directory.Delete(_feedDir, true);
        }
        catch
        {
            /* ignored */
        }

        try
        {
            if (!string.IsNullOrEmpty(_nugetConfigPath) && File.Exists(_nugetConfigPath)) File.Delete(_nugetConfigPath);
        }
        catch
        {
            /* ignored */
        }

        try
        {
            if (!string.IsNullOrEmpty(_tempGlobalJsonPath) && File.Exists(_tempGlobalJsonPath)) File.Delete(_tempGlobalJsonPath);
        }
        catch
        {
            /* ignored */
        }
    }
}