using System.CommandLine;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Slncs;
using SlncsGen;

// your fluent DSL library

var slncsFile = new Option<string>(name: "--slncs")
{
    Description = "The .slncs file to generate",
    Required = true
};

var outFile = new Option<string>(name: "--out")
{
    Description = "The output file",
    Required = true
};


slncsFile.Description = "Path to .slncs.cs";
outFile.Description = "Path to emit .slnx";

var root = new RootCommand { slncsFile, outFile };
var parseResult = root.Parse(args);

if (parseResult.Errors.Count > 0)
{
    foreach (var e in parseResult.Errors)
        Console.Error.WriteLine(e.Message);
    Console.Error.WriteLine("Usage: --slncs <file> --out <output.slnx>");
    Environment.ExitCode = 1;
    return;
}

var parsedSlncsFile = parseResult.GetValue(slncsFile)!;
var parsedOutFile = parseResult.GetValue(outFile)!;

try
{
    if (!File.Exists(parsedSlncsFile))
    {
        Console.Error.WriteLine($"Input script not found: {parsedSlncsFile}");
        Environment.ExitCode = 2;
        return;
    }

    var globals = new ScriptGlobals { OutputPath = parsedOutFile };
    var opts = ScriptOptions.Default
        .AddReferences(typeof(Solution).Assembly)
        .AddImports("Slncs");

    var code = File.ReadAllText(parsedSlncsFile);
    await CSharpScript.RunAsync(code, opts, globals);
}
catch (CompilationErrorException cex)
{
    Console.Error.WriteLine("Script compilation failed:");
    foreach (var d in cex.Diagnostics)
        Console.Error.WriteLine(d.ToString());
    Environment.ExitCode = 3;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    Environment.ExitCode = 4;
}