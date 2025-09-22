using System.Xml.Linq;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Slncs.Sdk;


/// <summary>
/// MSBuild task that invokes the Slncs generator (<c>SlncsGen.dll</c>) on a C# solution script
/// to produce a <c>.slnx</c> file and (optionally) an aggregator project listing all referenced projects.
/// </summary>
public sealed class SlncsExec : Task
{
    /// <summary>Path to the input C# solution script file (<c>.slncs</c> or <c>.slncs.cs</c>).</summary>
    [Required] public string SlncsFile { get; set; } = string.Empty;
    /// <summary>Destination path for the generated <c>.slnx</c> file.</summary>
    [Required] public string OutFile { get; set; } = string.Empty;
    /// <summary>Path to the compiled generator assembly (<c>SlncsGen.dll</c>).</summary>
    [Required] public string GeneratorDll { get; set; } = string.Empty;
    
    /// <inheritdoc />
    public override bool Execute()
    {
        var exit = SlncsRunner.Run(
            SlncsFile,
            OutFile,
            GeneratorDll,
            stdout: new LogWriter(this, MessageImportance.Low),
            stderr: new LogWriter(this, MessageImportance.High));

        if (exit != 0)
            Log.LogError($"SlncsGen exited with code {exit}.");

        var ok = exit == 0 && File.Exists(OutFile);
        if (!ok)
        {
            Log.LogError($"Generator did not produce output: {OutFile}");
            return false;
        }

        try
        {
            GenerateAggregator(OutFile);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to create aggregator project: {ex.Message}");
        }

        return true;
    }

    
    /// <summary>
    /// Creates a lightweight no-targets aggregator project (<c>.slnx.proj</c>) referencing all
    /// discovered project paths inside the generated solution. This enables a single MSBuild invocation
    /// (e.g. for IDE loading or building everything) without parsing the <c>.slnx</c> file again.
    /// </summary>
    /// <param name="slnxPath">Absolute path to the generated <c>.slnx</c>.</param>
    private void GenerateAggregator(string slnxPath)
    {
        if (!File.Exists(slnxPath)) return;
        var wrapperDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(slnxPath)!, ".."));
        var doc = XDocument.Load(slnxPath);
        var projects = doc.Root?.Elements("Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => Path.GetFullPath(Path.Combine(wrapperDir, p!)))
            .Where(File.Exists)
            .ToList() ?? [];
        
        if (projects.Count == 0) return;

        var aggregatorPath = slnxPath + ".proj";
        using var sw = new StreamWriter(aggregatorPath, false);
        sw.WriteLine("<Project Sdk=\"Microsoft.Build.NoTargets/3.6.0\">");
        sw.WriteLine("  <ItemGroup>");
        
        foreach (var rel in projects.Select(abs => Rel(abs).Replace("\\", "/")))
            sw.WriteLine($"    <ProjectReference Include=\"{rel}\" />");

        sw.WriteLine("  </ItemGroup>");
        sw.WriteLine("</Project>");
        Log.LogMessage(MessageImportance.Low, $"Created aggregator: {aggregatorPath}");
        return;

        string Rel(string abs) => abs.StartsWith(wrapperDir, StringComparison.OrdinalIgnoreCase)
            ? abs[wrapperDir.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : abs;
    }

    private sealed class LogWriter(Task task, MessageImportance importance) : StringWriter
    {
        public override void WriteLine(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            task.Log.LogMessage(importance, value);
        }
    }
}