using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Xml.Linq;
using JetBrains.Annotations;
using Task = Microsoft.Build.Utilities.Task;

namespace Slncs.Sdk;

/// <summary>
/// MSBuild task that parses a generated <c>.slnx</c> file and emits its referenced project files
/// as <see cref="ITaskItem"/>s so they can be forwarded to subsequent <c>MSBuild</c> invocations.
/// </summary>
/// <remarks>
/// The task expects the <c>.slnx</c> layout produced by the Slncs generator: a root <c>&lt;Solution&gt;</c>
/// element with zero or more <c>&lt;Project Path="relative/path.csproj" /&gt;</c> child nodes.
/// </remarks>
[UsedImplicitly]
public sealed class SlnxParse : Task
{
    /// <summary>The absolute path to the <c>.slnx</c> file to parse.</summary>
    [Required] public required string SlnxFile { get; set; } = string.Empty;

    /// <summary>
    /// The list of distinct project files discovered in the <see cref="SlnxFile"/> that physically
    /// exist on disk. Missing project paths are logged but excluded from the output.
    /// </summary>
    [Output] public ITaskItem[] Projects { get; set; } = [];

    /// <inheritdoc />
    public override bool Execute()
    {
        if (string.IsNullOrWhiteSpace(SlnxFile) || !File.Exists(SlnxFile))
        {
            Log.LogError($"Slnx file not found: {SlnxFile}");
            return false;
        }
        try
        {
            var objDir = Path.GetDirectoryName(Path.GetFullPath(SlnxFile))!; // obj folder
            var wrapperDir = Path.GetFullPath(Path.Combine(objDir, ".."));   // wrapper project directory
            var doc = XDocument.Load(SlnxFile);
            var raw = doc.Root?.Elements("Project").Select(e => e.Attribute("Path")?.Value).ToList() ?? new();
            foreach (var r in raw)
                Log.LogMessage(MessageImportance.High, $"[slncs-parse] Found entry Path='{r}'");
            var projs = raw
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(p => Path.GetFullPath(Path.Combine(wrapperDir, p!)))
                .Select(abs => new { abs, exists = File.Exists(abs) })
                .ToList();
            foreach (var item in projs)
                Log.LogMessage(MessageImportance.High, $"[slncs-parse] Candidate '{item.abs}' Exists={item.exists}");
            Projects = projs.Where(p => p.exists)
                .Select(p => (ITaskItem)new TaskItem(p.abs))
                .ToArray() ?? Array.Empty<ITaskItem>();
            Log.LogMessage(MessageImportance.Low, $"Parsed {Projects.Length} project(s) from {SlnxFile}");
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, true);
            return false;
        }
    }
}
