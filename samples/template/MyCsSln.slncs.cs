using Slncs;

Solution.Create()
    .Folder("/Solution Items", f => f.Files("Directory.Build.props"))
    .Project(@"src/ClassLibrary1/ClassLibrary1.csproj")
    .Project(@"src/ConsoleApp1/ConsoleApp1.csproj")
    .Write(OutputPath);