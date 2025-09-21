using System.CommandLine;
using System.Diagnostics;
using System.Text;

internal static class Program
{
    private static readonly Argument<FileInfo> ScriptOrWrapperArg = new("script-or-wrapper")
    {
        Description = ".slncs C# DSL script OR existing XML wrapper .slncs"
    };

    private static RootCommand BuildRoot()
    {
        var root = new RootCommand("Build a Slncs C# solution script without manually authoring the XML wrapper.");
        root.Add(ScriptOrWrapperArg);
        root.TreatUnmatchedTokensAsErrors = false; // allow /p: or -p: msbuild property overrides to flow through
        return root;
    }

    public static int Main(string[] args)
    {
        var root = BuildRoot();
        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var e in parseResult.Errors)
                Console.Error.WriteLine(e.Message);
            return 1;
        }

        var file = parseResult.GetValue(ScriptOrWrapperArg);
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
    }

    private static int ProcessPath(string path, string[] extraArgs)
    {
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
            catch
            {
            }

            return false;
        }

        if (IsXmlWrapper(path))
        {
            Console.WriteLine($"[slncs-build] Detected wrapper project: {Path.GetFileName(path)}");
            return RunDotnetBuild(path, extraArgs);
        }

        if (!string.Equals(Path.GetExtension(path), ".slncs", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Input must have .slncs extension (pure C# DSL script) or a wrapper .slncs XML project.");
            return 2;
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

        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Slncs.Sdk\">");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine($"    <SlncsFile>{path}</SlncsFile>");
        sb.AppendLine($"    <GeneratedSlnx>{slnxOut}</GeneratedSlnx>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine("</Project>");
        File.WriteAllText(wrapperPath, sb.ToString());

        Console.WriteLine($"[slncs-build] Generated transient wrapper: {wrapperPath}");
        Console.WriteLine($"[slncs-build] Building script: {path}");

        var exit = RunDotnetBuild(wrapperPath, extraArgs, scriptDir);
        Console.WriteLine($"[slncs-build] Exit code: {exit}");
        return exit;
    }

    private static int RunDotnetBuild(string projectFile, string[] extraArgs, string? workingDir = null)
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
}