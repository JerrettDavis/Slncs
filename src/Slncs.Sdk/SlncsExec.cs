using System.Xml.Linq;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Slncs.Sdk;

public sealed class SlncsExec : Task
{
    [Required] public string SlncsFile { get; set; } = string.Empty;
    [Required] public string OutFile { get; set; } = string.Empty;
    [Required] public string GeneratorDll { get; set; } = string.Empty;

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
            .ToList() ?? new();
        if (projects.Count == 0) return;

        string Rel(string abs) => abs.StartsWith(wrapperDir, StringComparison.OrdinalIgnoreCase)
            ? abs.Substring(wrapperDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : abs;

        var aggregatorPath = slnxPath + ".proj";
        using var sw = new StreamWriter(aggregatorPath, false);
        sw.WriteLine("<Project Sdk=\"Microsoft.Build.NoTargets/3.6.0\">");
        sw.WriteLine("  <ItemGroup>");
        foreach (var abs in projects)
        {
            var rel = Rel(abs).Replace("\\", "/");
            sw.WriteLine($"    <ProjectReference Include=\"{rel}\" />");
        }

        sw.WriteLine("  </ItemGroup>");
        sw.WriteLine("</Project>");
        Log.LogMessage(MessageImportance.Low, $"Created aggregator: {aggregatorPath}");
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