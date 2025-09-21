namespace SlncsGen;

/// <summary>
/// Globals passed into the Slncs C# script at execution time. The script writes the
/// generated solution using <see cref="OutputPath"/> as the target <c>.slnx</c> file.
/// </summary>
public class ScriptGlobals
{
    /// <summary>
    /// The fully qualified path where the generator should write the produced <c>.slnx</c> file.
    /// Provided by the hosting generator runner.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;
}
