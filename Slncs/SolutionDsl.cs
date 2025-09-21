using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Slncs;

public sealed class Solution
{
    public static SolutionBuilder Create() => new();
}

public sealed class SolutionBuilder
{
    private readonly List<Entry> _entries = new();

    public SolutionBuilder Project(string path)
    {
        _entries.Add(new ProjectEntry(Norm(path)));
        return this;
    }

    public SolutionBuilder Folder(string name, Action<FolderBuilder> configure)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Folder name required", nameof(name));
        var fb = new FolderBuilder(name);
        configure(fb);
        _entries.Add(fb.Build());
        return this;
    }

    /// <summary>Return the XDocument for the .slnx.</summary>
    public XDocument Build()
    {
        var root = new XElement("Solution");

        foreach (var e in Coalesce(_entries))
        {
            root.Add(e.ToXElement());
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    /// <summary>Write the .slnx to disk (creates directory if needed).</summary>
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

    public sealed class FolderBuilder
    {
        private readonly string _name;
        private readonly List<FileEntry> _files = new();
        private readonly List<FolderEntry> _folders = new();

        internal FolderBuilder(string name) => _name = name;

        public FolderBuilder File(string path)
        {
            _files.Add(new FileEntry(path));
            return this;
        }

        public FolderBuilder Files(params string[] paths)
        {
            foreach (var p in paths) File(p);
            return this;
        }

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
