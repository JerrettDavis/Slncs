using System.CommandLine;
using System.Diagnostics;
using System.Text;

var fileArg = new Argument<string>("script-or-wrapper", description: ".slncs script containing C# DSL code OR existing XML wrapper project (.slncs)");

var root = new RootCommand("Build a Slncs C# solution script without manually authoring the XML wrapper.")
{
    fileArg
};

root.SetHandler((string path) =>
{
    if (string.IsNullOrWhiteSpace(path))
    {
        Console.Error.WriteLine("Path is required.");
        Environment.ExitCode = 2;
        return;
    }

    path = Path.GetFullPath(path);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        Environment.ExitCode = 2;
        return;
    }

    bool IsXmlWrapper(string p)
    {
        try
        {
            using var sr = new StreamReader(p);
            for (int i = 0; i < 5; i++)
            {
                var line = sr.ReadLine();
                if (line == null) break;
                line = line.TrimStart();
                if (line.Length == 0) continue;
                return line.StartsWith("<Project", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return false;
    }

    if (IsXmlWrapper(path))
    {
        Console.WriteLine($"[slncs-build] Detected wrapper project: {Path.GetFileName(path)}");
        var exit = RunDotnetBuild(path, Array.Empty<string>());
        Environment.ExitCode = exit;
        return;
    }

    if (!string.Equals(Path.GetExtension(path), ".slncs", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Input must have .slncs extension (pure C# DSL script). If you have a wrapper already, it must also end with .slncs.");
        Environment.ExitCode = 2;
        return;
    }

    var scriptDir = Path.GetDirectoryName(path)!;
    var baseName = Path.GetFileNameWithoutExtension(path);
    var objDir = Path.Combine(scriptDir, "obj");
    Directory.CreateDirectory(objDir);
    var slnxOut = Path.Combine(objDir, baseName + ".slnx");

    var tempDir = Path.Combine(scriptDir, "obj", ".slncs-build");
    Directory.CreateDirectory(tempDir);
    var wrapperName = baseName + ".wrapper.slncs";
    var wrapperPath = Path.Combine(tempDir, wrapperName);

    var wrapper = new StringBuilder();
    // Always rely on repo/global.json mapping for Slncs.Sdk version resolution.
    string projectSdk = "Slncs.Sdk";
    wrapper.AppendLine($"<Project Sdk=\"{projectSdk}\">");
    wrapper.AppendLine("  <PropertyGroup>");
    wrapper.AppendLine($"    <SlncsFile>{path}</SlncsFile>");
    wrapper.AppendLine($"    <GeneratedSlnx>{slnxOut}</GeneratedSlnx>");
    wrapper.AppendLine("  </PropertyGroup>");
    wrapper.AppendLine("</Project>");
    File.WriteAllText(wrapperPath, wrapper.ToString());

    Console.WriteLine($"[slncs-build] Generated transient wrapper: {wrapperPath}");
    Console.WriteLine($"[slncs-build] Building script: {path}");

    var extraArgs = Environment.GetCommandLineArgs().Skip(2).ToArray();
    var exitCode = RunDotnetBuild(wrapperPath, extraArgs, scriptDir);

    Console.WriteLine($"[slncs-build] Exit code: {exitCode}");
    Environment.ExitCode = exitCode;
}, fileArg);

await root.InvokeAsync(args);

static int RunDotnetBuild(string projectFile, string[] extraArgs, string? workingDir = null)
{
    var psi = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = workingDir ?? Path.GetDirectoryName(projectFile)!,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    psi.ArgumentList.Add("build");
    psi.ArgumentList.Add(projectFile);
    psi.ArgumentList.Add("-v:m");
    psi.ArgumentList.Add("--nologo");
    foreach (var a in extraArgs)
    {
        if (string.Equals(a, "slncs-build", StringComparison.OrdinalIgnoreCase)) continue;
        psi.ArgumentList.Add(a);
    }
    using var proc = Process.Start(psi)!;
    proc.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
    proc.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
    proc.BeginOutputReadLine();
    proc.BeginErrorReadLine();
    proc.WaitForExit();
    return proc.ExitCode;
}
