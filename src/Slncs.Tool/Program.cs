using System.CommandLine;
using System.Diagnostics;
using System.Text;


Argument<FileInfo> scriptOrWrapperArg = new("script-or-wrapper")
{
    Description = ".slncs C# DSL script OR existing XML wrapper .slncs"
};

var root = BuildRoot(scriptOrWrapperArg);
var parseResult = root.Parse(args);
if (parseResult.Errors.Count > 0)
{
    foreach (var e in parseResult.Errors)
        Console.Error.WriteLine(e.Message);
    return 1;
}


var file = parseResult.GetValue(scriptOrWrapperArg);
if (file is null)
{
    Console.Error.WriteLine("Path is required.");
    return 2;
}

var path = file.FullName;
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 2;
}

// Determine extra build args: any unmatched tokens become pass-through to dotnet build.
var extraArgs = parseResult.UnmatchedTokens.ToArray();

try
{
    return ProcessPath(path, extraArgs);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[slncs-build] Unhandled error: {ex.Message}\n{ex}");
    return 99;
}

RootCommand BuildRoot(Argument<FileInfo> argument)
{
    var rootCommand = new RootCommand("Build a Slncs C# solution script without manually authoring the XML wrapper.") { argument };
    rootCommand.TreatUnmatchedTokensAsErrors = false; // allow /p: or -p: msbuild property overrides to flow through
    return rootCommand;
}


int ProcessPath(string inputPath, string[] arguments)
{
    if (IsXmlWrapper(inputPath))
    {
        Console.WriteLine($"[slncs-build] Detected wrapper project: {Path.GetFileName(inputPath)}");
        return RunDotnetBuild(inputPath, arguments);
    }

    if (!string.Equals(Path.GetExtension(inputPath), ".slncs", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Input must have .slncs extension (pure C# DSL script) or a wrapper .slncs XML project.");
        return 2;
    }

    var scriptDir = Path.GetDirectoryName(inputPath)!;
    var baseName = Path.GetFileNameWithoutExtension(inputPath);
    var objDir = Path.Combine(scriptDir, "obj");
    Directory.CreateDirectory(objDir);
    var slnxOut = Path.Combine(objDir, baseName + ".slnx");

    var tempDir = Path.Combine(scriptDir, "obj", ".slncs-build");
    Directory.CreateDirectory(tempDir);
    var wrapperName = baseName + ".wrapper.slncs";
    var wrapperPath = Path.Combine(tempDir, wrapperName);

    var sdkVersionOverride = Environment.GetEnvironmentVariable("SLNCS_E2E_SDK_VERSION");
    var sdkAttr = string.IsNullOrWhiteSpace(sdkVersionOverride) ? "Slncs.Sdk" : $"Slncs.Sdk/{sdkVersionOverride}";
    Console.WriteLine($"[slncs-build] Wrapper Sdk attr: {sdkAttr}");

    var sb = new StringBuilder();
    sb.AppendLine($"<Project Sdk=\"{sdkAttr}\">");
    sb.AppendLine("  <PropertyGroup>");
    sb.AppendLine($"    <SlncsFile>{inputPath}</SlncsFile>");
    sb.AppendLine($"    <GeneratedSlnx>{slnxOut}</GeneratedSlnx>");
    var e2eFeed = Environment.GetEnvironmentVariable("SLNCS_E2E_FEED");
    if (!string.IsNullOrEmpty(e2eFeed))
    {
        // Ensure local feed is searched first
        sb.AppendLine($"    <RestoreSources>{e2eFeed};https://api.nuget.org/v3/index.json</RestoreSources>");
    }

    sb.AppendLine("  </PropertyGroup>");
    sb.AppendLine("</Project>");
    File.WriteAllText(wrapperPath, sb.ToString());

    Console.WriteLine($"[slncs-build] Generated transient wrapper: {wrapperPath}");
    Console.WriteLine($"[slncs-build] Building script: {inputPath}");

    var exit = RunDotnetBuild(wrapperPath, arguments, scriptDir);
    Console.WriteLine($"[slncs-build] Exit code: {exit}");
    return exit;

    bool IsXmlWrapper(string p)
    {
        try
        {
            using var sr = new StreamReader(p);
            for (var i = 0; i < 5; i++)
            {
                var line = sr.ReadLine();
                if (line == null) break;
                line = line.TrimStart();
                if (line.Length == 0) continue;
                return line.StartsWith("<Project", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }
}

static int RunDotnetBuild(string projectFile, string[] extraArgs, string? workingDir = null)
{
    var psi = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = workingDir ?? Path.GetDirectoryName(projectFile)!,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
#if NETSTANDARD2_0
        var sb = new StringBuilder();
        sb.Append("build ");
        sb.Append(projectFile);
        sb.Append(" -v:m --nologo");
        foreach (var a in extraArgs.Where(a => !string.Equals(a, "slncs-build", StringComparison.OrdinalIgnoreCase)))
            sb.Append(" ").Append(a);
#else
    psi.ArgumentList.Add("build");
    psi.ArgumentList.Add(projectFile);
    psi.ArgumentList.Add("-v:m");
    psi.ArgumentList.Add("--nologo");
    foreach (var a in extraArgs)
    {
        if (string.Equals(a, "slncs-build", StringComparison.OrdinalIgnoreCase)) continue;
        psi.ArgumentList.Add(a);
    }
#endif
    using var proc = Process.Start(psi)!;
    proc.OutputDataReceived += (_, e) =>
    {
        if (e.Data != null) Console.WriteLine(e.Data);
    };
    proc.ErrorDataReceived += (_, e) =>
    {
        if (e.Data != null) Console.Error.WriteLine(e.Data);
    };
    proc.BeginOutputReadLine();
    proc.BeginErrorReadLine();
    proc.WaitForExit();
    return proc.ExitCode;
}