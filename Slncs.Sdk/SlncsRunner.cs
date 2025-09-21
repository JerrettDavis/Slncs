using System.Diagnostics;

namespace Slncs.Sdk;

/// <summary>
/// Lightweight process helper that executes the Slncs generator (<c>SlncsGen.dll</c>) via <c>dotnet exec</c>
/// and streams its stdout/stderr into provided writers. Performs basic validation and returns the
/// generator process exit code (or <c>2</c> for argument/file validation errors before launch).
/// </summary>
public static class SlncsRunner
{
    /// <summary>
    /// Execute the Slncs generator: <c>dotnet exec &lt;generatorDll&gt; --slncs &lt;slncsFile&gt; --out &lt;outFile&gt;</c>.
    /// </summary>
    /// <param name="slncsFile">Path to the C# solution script file.</param>
    /// <param name="outFile">Target path for the generated <c>.slnx</c>.</param>
    /// <param name="generatorDll">Path to the already built <c>SlncsGen.dll</c>.</param>
    /// <param name="stdout">Optional writer for standard output (defaults to <see cref="Console.Out"/>).</param>
    /// <param name="stderr">Optional writer for standard error (defaults to <see cref="Console.Error"/>).</param>
    /// <returns>
    /// Process exit code from the generator. Returns <c>2</c> if pre-flight validation fails
    /// (missing script / output path / generator). A non-zero return (other than 2) indicates
    /// the generator itself failed.
    /// </returns>
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