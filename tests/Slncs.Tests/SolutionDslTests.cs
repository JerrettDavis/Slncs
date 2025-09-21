using System.Xml.Linq;
using Slncs;

public class SolutionDslTests
{
    [Fact]
    public void Build_Emits_Expected_Minimal_Xml()
    {
        var xml = Solution.Create()
            .Folder("/Solution Items", f => f.Files("Directory.Build.props"))
            .Project(Path.Combine("src","A","A.csproj"))
            .Project(Path.Combine("src","B","B.csproj"))
            .Build();

        var expected = new XElement("Solution",
            new XElement("Folder", new XAttribute("Name","/Solution Items/"),
                new XElement("File", new XAttribute("Path","Directory.Build.props"))),
            new XElement("Project", new XAttribute("Path", Path.Combine("src","A","A.csproj")) ),
            new XElement("Project", new XAttribute("Path", Path.Combine("src","B","B.csproj")) )
        );

        Assert.True(XNode.DeepEquals(xml.Root, expected), "Generated XML should match expected structure.");
    }

    [Fact]
    public void Write_Creates_File()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "out.slnx");
        Solution.Create().Project(Path.Combine("src","A","A.csproj")).Write(tmp);

        Assert.True(File.Exists(tmp), "Write should create the .slnx file.");

        var doc = XDocument.Load(tmp);
        Assert.NotNull(doc.Root);
        Assert.Equal("Solution", doc.Root!.Name.LocalName);
    }
}