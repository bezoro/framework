namespace Bezoro.Core.CodeGen;

/// <summary>
///     Handles file output for generated C# code using the <see cref="CSharpCodeBuilder" />.
/// </summary>
/// <remarks>
///     Constructs a new file generator for an output path.
/// </remarks>
/// <param name="outputPath">The file path where generated code will be written.</param>
public class CSharpFileGenerator(string outputPath)
{
	private readonly CSharpCodeBuilder _builder    = new();
	private readonly string            _outputPath = outputPath;

	/// <summary>
	///     Returns the underlying <see cref="CSharpCodeBuilder" /> to be used for code construction.
	/// </summary>
	public CSharpCodeBuilder GetBuilder() =>
		_builder;

	/// <summary>
	///     Generates the C# file and writes it to disk at the output path.
	/// </summary>
	public void Generate()
	{
		string code = _builder.Generate();
		File.WriteAllText(_outputPath, code);
	}
}
