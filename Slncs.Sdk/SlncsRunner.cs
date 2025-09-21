using System;
using System.Diagnostics;
using System.IO;

namespace Slncs.Sdk;

public static class SlncsRunner
{
    /// <summary>Executes the generator: dotnet exec SlncsGen.dll --slncs &lt;in&gt; --out &lt;out&gt;.</summary>
    public static int Run(string slncsFile, string outFile, string generatorDll, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        if (string.IsNullOrWhiteSpace(slncsFile) || !File.Exists(slncsFile))
        {
            stderr.WriteLine($"SlncsRunner: SlncsFile not found: {slncsFile}");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(outFile))
        {
            stderr.WriteLine("SlncsRunner: OutFile must be provided.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(generatorDll) || !File.Exists(generatorDll))
        {
            stderr.WriteLine($"SlncsRunner: Generator not found: {generatorDll}");
            return 2;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(slncsFile))!
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(generatorDll);
        psi.ArgumentList.Add("--slncs");
        psi.ArgumentList.Add(slncsFile);
        psi.ArgumentList.Add("--out");
        psi.ArgumentList.Add(outFile);

        using var proc = Process.Start(psi)!;
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdout.WriteLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.WriteLine(e.Data);
        };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();
        return proc.ExitCode;
    }
}