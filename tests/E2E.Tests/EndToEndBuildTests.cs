using System.Diagnostics;
using Xunit.Abstractions;

namespace E2E.Tests;

public class EndToEndBuildTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _feedDir;
    private readonly string _config;
    private readonly string _nugetConfigPath;
    private readonly string _tempGlobalJsonPath;
    private readonly string _sdkVersionUsed;
    private readonly string _generatorDllPath; // local built generator dll

    public EndToEndBuildTests(ITestOutputHelper output)
    {
        _output = output;
        _config = DetectConfiguration();
        _feedDir = Path.Combine(RepoRoot(), "samples", ".e2e-local-feed");
        // Create local feed
        if (Directory.Exists(_feedDir))
        {
            try { Directory.Delete(_feedDir, true); } catch { }
        }
        Directory.CreateDirectory(_feedDir);
        var baseVersion = ReadBaseSdkVersion();
        _sdkVersionUsed = baseVersion + "-e2e-" + DateTime.UtcNow.Ticks.ToString();
        PackLocalSdk(_sdkVersionUsed);

        // Compute local generator dll path (from source tree) and ensure it exists (build if necessary)
        _generatorDllPath = Path.Combine(RepoRoot(), "src", "SlncsGen", "bin", _config, "net8.0", "SlncsGen.dll");
        if (!File.Exists(_generatorDllPath))
        {
            BuildLocalGenerator();
        }
        if (!File.Exists(_generatorDllPath))
        {
            throw new FileNotFoundException("Failed to build local SlncsGen.dll for E2E tests", _generatorDllPath);
        }
        _output.WriteLine($"[e2e] Using local generator dll: {_generatorDllPath}");

        // Write transient nuget.config at samples root
        var samplesRoot = Path.Combine(RepoRoot(), "samples");
        _nugetConfigPath = Path.Combine(samplesRoot, "nuget.config");
        var nugetConfig = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <packageSources>\n    <clear />\n    <add key=\"local-e2e\" value=\"{_feedDir}\" />\n    <add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\" />\n  </packageSources>\n</configuration>";
        File.WriteAllText(_nugetConfigPath, nugetConfig);
        // Write temporary global.json to samples root overriding msbuild-sdks mapping for Slncs.Sdk
        _tempGlobalJsonPath = Path.Combine(samplesRoot, "global.json");
        var tmpGlobal = $"{{\n  \"msbuild-sdks\": {{\n    \"Slncs.Sdk\": \"{_sdkVersionUsed}\"\n  }}\n}}";
        File.WriteAllText(_tempGlobalJsonPath, tmpGlobal);
        _output.WriteLine($"[e2e] Using temporary SDK version: {_sdkVersionUsed}");
    }

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
        using var p = Process.Start(psi)!; p.WaitForExit();
        if (p.ExitCode != 0)
        {
            _output.WriteLine("Failed to build SlncsGen. StdOut:\n" + p.StandardOutput.ReadToEnd());
            _output.WriteLine("StdErr:\n" + p.StandardError.ReadToEnd());
        }
    }

    private static string DetectConfiguration()
    {
        var bin = AppContext.BaseDirectory;
        return bin.IndexOf(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0 ? "Release" : "Debug";
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
        using var p = Process.Start(psi)!; p.WaitForExit();
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
        catch { }
        return "0.1.0";
    }

    [Fact]
    public void Dotnet_Build_Works_On_Slncs_Wrapper()
    {
        var root = RepoRoot();
        var samplesBaseDir = Path.Combine(root, "samples");
        var samplesDir = Path.Combine(samplesBaseDir, "template");
        var wrapper = Path.Combine(samplesDir, "MyCsSln.slncs");
        Assert.True(File.Exists(wrapper), "Sample wrapper must exist.");

        var psi = new ProcessStartInfo("dotnet", $"build \"{wrapper}\" -c {_config} -v:m --nologo")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = samplesDir
        };
        psi.Environment["SLNCS_GEN_DLL"] = _generatorDllPath;
        var p = Process.Start(psi)!;
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        if (p.ExitCode != 0)
        {
            _output.WriteLine("--- wrapper stdout ---\n" + stdout);
            _output.WriteLine("--- wrapper stderr ---\n" + stderr);
        }

        Assert.Equal(0, p.ExitCode);
        _output.WriteLine(stdout);

        var slnx = Path.Combine(samplesDir, "obj", "MyCsSln.slnx");
        Assert.True(File.Exists(slnx), ".slnx should be generated by wrapper.");

        var consoleDll = Path.Combine(samplesDir, "src", "ConsoleApp1", "bin", _config, "net8.0", "ConsoleApp1.dll");
        if (!File.Exists(consoleDll))
        {
            _output.WriteLine("ConsoleApp1.dll missing; dumping directory tree under samples/src:");
            foreach (var path in Directory.EnumerateFiles(Path.Combine(samplesDir, "src"), "*", SearchOption.AllDirectories))
                _output.WriteLine(path);
        }

        Assert.True(File.Exists(consoleDll), "Console application should have been built by direct parse pipeline.");
    }

    [Fact]
    public void Slncs_Build_Tool_Works_On_Pure_Script()
    {
        var root = RepoRoot();
        var samplesBaseDir = Path.Combine(root, "samples");
        var pureDir = Path.Combine(samplesBaseDir, "pure");
        var toolDir = Path.Combine(root, "src", "Slncs.Tool");
        Assert.True(Directory.Exists(toolDir), "Tool project must exist");

        var pureScript = Path.Combine(pureDir, "MyCsSlnSingle.slncs");
        File.WriteAllText(pureScript,
            "using Slncs;\nSolution.Create()\n    .Folder(\"/Solution Items\", f => f.Files(\"Directory.Build.props\"))\n    .Project(@\"../template/src/ClassLibrary1/ClassLibrary1.csproj\")\n    .Project(@\"../template/src/ConsoleApp1/ConsoleApp1.csproj\")\n    .Write(OutputPath);\n");

        var psi = new ProcessStartInfo("dotnet", $"run --project \"{toolDir}\" -c {_config} -- \"{pureScript}\"")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = pureDir
        };
        psi.Environment["SLNCS_GEN_DLL"] = _generatorDllPath;
        var p = Process.Start(psi)!;
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        if (p.ExitCode != 0)
        {
            _output.WriteLine("--- tool stdout ---\n" + stdout);
            _output.WriteLine("--- tool stderr ---\n" + stderr);
        }

        Assert.Equal(0, p.ExitCode);
        _output.WriteLine(stdout);

        var slnx = Path.Combine(pureDir, "obj", "MyCsSlnSingle.slnx");
        Assert.True(File.Exists(slnx), ".slnx should be generated for pure script.");

        var consoleDll = Path.Combine(samplesBaseDir, "template", "src", "ConsoleApp1", "bin", _config, "net8.0", "ConsoleApp1.dll");
        Assert.True(File.Exists(consoleDll), "Console application should have been built by slncs-build tool.");

        try { File.Delete(pureScript); } catch { }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_feedDir)) Directory.Delete(_feedDir, true);
        }
        catch { }
        try
        {
            if (!string.IsNullOrEmpty(_nugetConfigPath) && File.Exists(_nugetConfigPath)) File.Delete(_nugetConfigPath);
        }
        catch { }
        try
        {
            if (!string.IsNullOrEmpty(_tempGlobalJsonPath) && File.Exists(_tempGlobalJsonPath)) File.Delete(_tempGlobalJsonPath);
        }
        catch { }
    }
}