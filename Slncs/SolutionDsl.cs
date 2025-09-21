using System.Xml.Linq;

namespace Slncs;

/// <summary>
/// Entry point for constructing a solution description using a fluent C# DSL.
/// </summary>
/// <remarks>
/// A <c>Solution</c> is materialized into a compact <c>.slnx</c> XML format that lists
/// projects, logical folders and loose files. Use <see cref="Create"/> to begin and
/// chain <see cref="SolutionBuilder"/> calls. Finally call <see cref="SolutionBuilder.Write"/>
/// (or <see cref="SolutionBuilder.Build"/> to obtain an <see cref="XDocument"/>).
/// </remarks>
public sealed class Solution
{
    /// <summary>
    /// Create a new mutable <see cref="SolutionBuilder"/> instance to describe a solution.
    /// </summary>
    public static SolutionBuilder Create() => new();
}

/// <summary>
/// Fluent builder for describing solution contents (projects, folders, files) and writing a <c>.slnx</c>.
/// </summary>
/// <remarks>
/// The builder de-duplicates identical entries (same project path, file path, folder name) and
/// produces a stable, sorted XML representation to aid reproducible builds and diffing.
/// </remarks>
public sealed class SolutionBuilder
{
    private readonly List<Entry> _entries = new();

    /// <summary>
    /// Add a project reference to the solution.
    /// </summary>
    /// <param name="path">Relative (recommended) or absolute path to a <c>.csproj</c> (or other MSBuild project).</param>
    /// <returns>The current <see cref="SolutionBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentException">If <paramref name="path"/> is null or whitespace.</exception>
    public SolutionBuilder Project(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Project path required", nameof(path));
        _entries.Add(new ProjectEntry(Norm(path)));
        return this;
    }

    /// <summary>
    /// Add a logical folder (which may contain files and subfolders) to the solution.
    /// </summary>
    /// <param name="name">Display name of the folder (a trailing slash is optional).</param>
    /// <param name="configure">Delegate that populates the folder via a nested <see cref="FolderBuilder"/>.</param>
    /// <returns>The current <see cref="SolutionBuilder"/> for chaining.</returns>
    /// <exception cref="ArgumentException">If <paramref name="name"/> is null or whitespace.</exception>
    public SolutionBuilder Folder(string name, Action<FolderBuilder> configure)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Folder name required", nameof(name));
        var fb = new FolderBuilder(name);
        configure(fb);
        _entries.Add(fb.Build());
        return this;
    }

    /// <summary>Return the in-memory XML (<see cref="XDocument"/>) representing the solution.</summary>
    /// <remarks>
    /// Call <see cref="Write"/> to persist to disk. The output XML root element is <c>&lt;Solution&gt;</c>.
    /// </remarks>
    public XDocument Build()
    {
        var root = new XElement("Solution");
        foreach (var e in Coalesce(_entries))
            root.Add(e.ToXElement());
        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    /// <summary>
    /// Generate the <c>.slnx</c> file at the specified path (directory is created if necessary).
    /// </summary>
    /// <param name="slnxPath">Path to write. If it does not end with <c>.slnx</c>, the extension is appended.</param>
    public void Write(string slnxPath)
    {
        if (!slnxPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            slnxPath += ".slnx";
        var doc = Build();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(slnxPath))!);
        using var sw = new StreamWriter(slnxPath);
        doc.Save(sw);
    }

    private static string Norm(string p) => p.Replace('\\', Path.DirectorySeparatorChar)
                                             .Replace('/', Path.DirectorySeparatorChar);

    private static IEnumerable<Entry> Coalesce(IEnumerable<Entry> entries)
        => entries
            .GroupBy(e => e.Key)
            .Select(g => g.First())
            .OrderBy(e => e.SortKey);

    // ----- Internal model -----

    internal abstract record Entry(string Key, string SortKey)
    {
        public abstract XElement ToXElement();
    }

    private sealed record ProjectEntry(string Path) : Entry(
        Key: $"P|{Path}",
        SortKey: $"1|{Path}")
    {
        public override XElement ToXElement() => new("Project", new XAttribute("Path", Path));
    }

    internal sealed record FolderEntry(string Name, IReadOnlyList<FileEntry> Files, IReadOnlyList<FolderEntry> Subfolders)
        : Entry(Key: $"F|{Name}", SortKey: $"0|{Name}")
    {
        public override XElement ToXElement()
        {
            var el = new XElement("Folder", new XAttribute("Name", Name.EndsWith("/") ? Name : Name + "/"));
            foreach (var f in Files.OrderBy(f => f.Path))
                el.Add(f.ToXElement());
            foreach (var sf in Subfolders.OrderBy(sf => sf.Name, StringComparer.OrdinalIgnoreCase))
                el.Add(sf.ToXElement());
            return el;
        }
    }

    internal sealed record FileEntry(string Path) : Entry(
        Key: $"FI|{Path}",
        SortKey: $"2|{Path}")
    {
        public override XElement ToXElement() => new("File", new XAttribute("Path", Path));
    }

    /// <summary>
    /// Fluent builder for a logical solution folder (may contain files and child folders).
    /// </summary>
    public sealed class FolderBuilder
    {
        private readonly string _name;
        private readonly List<FileEntry> _files = new();
        private readonly List<FolderEntry> _folders = new();

        internal FolderBuilder(string name) => _name = name;

        /// <summary>Add a single file entry to the folder.</summary>
        /// <param name="path">Relative or absolute path to a file.</param>
        public FolderBuilder File(string path)
        {
            _files.Add(new FileEntry(path));
            return this;
        }

        /// <summary>Add multiple file entries in one call.</summary>
        /// <param name="paths">File paths to add.</param>
        public FolderBuilder Files(params string[] paths)
        {
            foreach (var p in paths) File(p);
            return this;
        }

        /// <summary>Create and configure a nested child folder.</summary>
        /// <param name="name">Child folder name.</param>
        /// <param name="configure">Delegate to populate the child folder.</param>
        public FolderBuilder Folder(string name, Action<FolderBuilder> configure)
        {
            var child = new FolderBuilder(name);
            configure(child);
            _folders.Add(child.Build());
            return this;
        }

#if NET6_0_OR_GREATER        
        internal FolderEntry Build() => new(_name, _files.DistinctBy(f => f.Path).ToList(), _folders);
#else
        internal FolderEntry Build() => new(_name, DistinctBy(_files, f => f.Path).ToList(), _folders);        
        
        private static IEnumerable<T> DistinctBy<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector)
            => source.GroupBy(keySelector).Select(g => g.First());       
#endif
    }
}
