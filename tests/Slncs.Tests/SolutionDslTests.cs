using System.Xml.Linq;
using Slncs;

public class SolutionDslTests
{
    [Fact]
    public void Build_Emits_Expected_Minimal_Xml()
    {
        var xml = Solution.Create()
            .Folder("/Solution Items", f => f.Files("Directory.Build.props"))
            .Project(@"src\A\A.csproj")
            .Project(@"src\B\B.csproj")
            .Build();

        var expected = XElement.Parse(@"
<Solution>
  <Folder Name=""/Solution Items/"">
    <File Path=""Directory.Build.props"" />
  </Folder>
  <Project Path=""src\A\A.csproj"" />
  <Project Path=""src\B\B.csproj"" />
</Solution>");

        Assert.True(XNode.DeepEquals(xml.Root, expected), "Generated XML should match expected structure.");
    }

    [Fact]
    public void Write_Creates_File()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "out.slnx");
        Solution.Create().Project(@"src\A\A.csproj").Write(tmp);

        Assert.True(File.Exists(tmp), "Write should create the .slnx file.");

        var doc = XDocument.Load(tmp);
        Assert.NotNull(doc.Root);
        Assert.Equal("Solution", doc.Root!.Name.LocalName);
    }
}