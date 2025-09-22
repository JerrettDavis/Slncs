using System.Xml.Linq;
using TinyBDD;
using TinyBDD.Assertions;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace Slncs.Tests;

[Feature("Solution DSL")]
public class SolutionDslTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Fact]
    [Scenario("Given a valid solution, when built, then generated XML should be valid")]
    public Task GivenAValidSolution_WhenBuilt_ThenGeneratedXmlShouldBeValid()
        => Given("a valid solution", () => Solution.Create()
                .Folder("/Solution Items", f => f.Files("Directory.Build.props"))
                .Project(Path.Combine("src", "A", "A.csproj"))
                .Project(Path.Combine("src", "B", "B.csproj")))
            .When("built", s => Task.FromResult(s.Build()))
            .Then("the generated XML should be valid", xml =>
            {
                Assert.NotNull(xml.Root);
                Assert.Equal("Solution", xml.Root!.Name.LocalName);
            })
            .And("should match expected structure", xml =>
            {
                var element = new XElement("Solution",
                    new XElement("Folder", new XAttribute("Name", "/Solution Items/"),
                        new XElement("File", new XAttribute("Path", "Directory.Build.props"))),
                    new XElement("Project", new XAttribute("Path", Path.Combine("src", "A", "A.csproj"))),
                    new XElement("Project", new XAttribute("Path", Path.Combine("src", "B", "B.csproj")))
                );

                Expect.For(XNode.DeepEquals(xml.Root, element)).ToBeTrue();
            })
            .AssertPassed();

    [Fact]
    [Scenario("Given a valid solution, when written, then file should be created")]
    public Task GivenAValidSolution_WhenWritten_ThenFileShouldBeCreated()
        => Given("a valid solution", () => Solution.Create().Project(Path.Combine("src", "A", "A.csproj")))
            .When("written to a temp file", s =>
            {
                var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "out.slnx");
                s.Write(tmp);
                return tmp;
            })
            .Then("the file should exist", path => Assert.True(File.Exists(path)))
            .And("the file should contain valid XML", path =>
            {
                var doc = XDocument.Load(path);
                Assert.NotNull(doc.Root);
                Assert.Equal("Solution", doc.Root!.Name.LocalName);
            })
            .AssertPassed();
}