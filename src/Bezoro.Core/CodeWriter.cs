using System.Text;

namespace Bezoro.Core;

/// <summary>
/// Provides methods for generating structured, indented code with automatic "using" header collection and scope management.
/// </summary>
public class CodeWriter
{
	private const    string          _INDENT_STRING = "    "; // 4 spaces
	private readonly HashSet<string> _usings        = new();

	private readonly StringBuilder _builder   = new();
	private          bool          _isNewLine = true;

	private int _indentLevel;

	/// <summary>
	/// Returns the generated code as a string, including using statements and a file header.
	/// </summary>
	public override string ToString()
	{
		var finalBuilder = new StringBuilder();

		// Write file header
		finalBuilder.AppendLine("// This file is auto-generated. Do not modify.");
		finalBuilder.AppendLine();

		// Write usings
		foreach (string? @using in _usings.OrderBy(x => x)) finalBuilder.AppendLine($"using {@using};");

		if (_usings.Any()) finalBuilder.AppendLine();

		// Write main content
		finalBuilder.Append(_builder.ToString());
		return finalBuilder.ToString();
	}

	/// <summary>
	/// Adds a using directive for the specified namespace.
	/// </summary>
	/// <param name="nameSpace">The namespace to add as a using directive.</param>
	/// <returns>This <see cref="CodeWriter"/> instance for chaining.</returns>
	public CodeWriter AddUsing(string nameSpace)
	{
		_usings.Add(nameSpace);
		return this;
	}

	/// <summary>
	/// Adds multiple using directives for the specified namespaces.
	/// </summary>
	/// <param name="namespaces">The collection of namespaces to add as using directives.</param>
	/// <returns>This <see cref="CodeWriter"/> instance for chaining.</returns>
	public CodeWriter AddUsingRange(IEnumerable<string> namespaces)
	{
		foreach (string? @namespace in namespaces) AddUsing(@namespace);
		return this;
	}

	/// <summary>
	/// Writes the specified text, applying indentation if it is the beginning of a new line.
	/// </summary>
	/// <param name="text">The text to write.</param>
	/// <returns>This <see cref="CodeWriter"/> instance for chaining.</returns>
	public CodeWriter Write(string text)
	{
		if (_isNewLine)
		{
			WriteIndent();
			_isNewLine = false;
		}

		_builder.Append(text);
		return this;
	}

	/// <summary>
	/// Writes a line of text with the current indentation, or a blank line if the input is empty.
	/// </summary>
	/// <param name="line">The line to write.</param>
	/// <returns>This <see cref="CodeWriter"/> instance for chaining.</returns>
	public CodeWriter WriteLine(string line = "")
	{
		if (!string.IsNullOrEmpty(line))
		{
			WriteIndent();
			_builder.AppendLine(line);
		}
		else
		{
			_builder.AppendLine();
		}

		_isNewLine = true;
		return this;
	}

	/// <summary>
	/// Begins a new code scope, optionally starting with a declaration (e.g., an if/for/class signature).
	/// </summary>
	/// <param name="declaration">The declaration line to write before the opening brace, or null.</param>
	/// <returns>An <see cref="IDisposable"/> that will end the scope upon disposal.</returns>
	public IDisposable BeginScope(string? declaration = null)
	{
		if (declaration != null) WriteLine(declaration);

		WriteLine("{");
		_indentLevel++;
		return new ScopeGuard(this);
	}

	/// <summary>
	/// Ends the current code scope, writing the closing brace or specified closer.
	/// </summary>
	/// <param name="closer">The string to write as the closer, typically "}".</param>
	public void EndScope(string closer = "}")
	{
		_indentLevel--;
		WriteLine(closer);
	}

	/// <summary>
	/// Appends indentation to the output according to the current scope level.
	/// </summary>
	private void WriteIndent()
	{
		for (var i = 0; i < _indentLevel; i++) _builder.Append(_INDENT_STRING);
	}

	/// <summary>
	/// Helper type for automatically closing scopes (writes ending '}' when disposed).
	/// </summary>
	private class ScopeGuard : IDisposable
	{
		private readonly CodeWriter _writer;

		/// <summary>
		/// Constructs a new <see cref="ScopeGuard"/> for the specified writer.
		/// </summary>
		/// <param name="writer">The writer for which to manage the scope.</param>
		public ScopeGuard(CodeWriter writer)
		{
			_writer = writer;
		}

		/// <summary>
		/// Ends the current scope on the writer when disposed.
		/// </summary>
		public void Dispose() =>
			_writer.EndScope();
	}
}

/// <summary>
/// Provides higher-level programmatic construction and composition of C# code constructs, using <see cref="CodeWriter"/> underneath.
/// </summary>
public class CSharpCodeBuilder
{
	private readonly CodeWriter    _writer         = new();
	private readonly Stack<string> _namespaceStack = new();
	private          bool          _isInClass;

	/// <summary>
	/// Adds a field declaration to the current class.
	/// </summary>
	/// <param name="name">The name of the field.</param>
	/// <param name="type">The type of the field.</param>
	/// <param name="accessibility">The accessibility modifier (e.g., private/protected/public).</param>
	/// <param name="isReadonly">Whether the field is readonly.</param>
	/// <param name="initialValue">Optional initializer value as code.</param>
	/// <returns>This <see cref="CSharpCodeBuilder"/> instance for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if not in a class definition.</exception>
	public CSharpCodeBuilder AddField(
		string  name,
		string  type,
		string  accessibility = "private",
		bool    isReadonly    = false,
		string? initialValue  = null
	)
	{
		if (!_isInClass) throw new InvalidOperationException("Must be in a class to add a field");

		var declaration = new StringBuilder();
		declaration.Append($"{accessibility} ");

		if (isReadonly) declaration.Append("readonly ");

		declaration.Append($"{type} {name}");

		if (initialValue != null) declaration.Append($" = {initialValue}");

		declaration.Append(";");

		_writer.WriteLine(declaration.ToString());
		return this;
	}

	/// <summary>
	/// Adds a method declaration to the current class.
	/// </summary>
	/// <param name="name">The method name.</param>
	/// <param name="returnType">The return type.</param>
	/// <param name="accessibility">Accessibility modifier.</param>
	/// <param name="isStatic">Whether the method is static.</param>
	/// <param name="parameters">Parameter list as code fragments.</param>
	/// <param name="bodyWriter">Callback that writes the method body.</param>
	/// <param name="attributes">Optional attributes for the method.</param>
	/// <returns>This <see cref="CSharpCodeBuilder"/> instance for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if not in a class definition.</exception>
	public CSharpCodeBuilder AddMethod(
		string              name,
		string              returnType    = "void",
		string              accessibility = "public",
		bool                isStatic      = false,
		string[]?           parameters    = null,
		Action<CodeWriter>? bodyWriter    = null,
		string[]?           attributes    = null
	)
	{
		if (!_isInClass) throw new InvalidOperationException("Must be in a class to add a method");

		// Write attributes
		if (attributes != null)
			foreach (string attribute in attributes)
				_writer.WriteLine($"[{attribute}]");

		var declaration = new StringBuilder();
		declaration.Append($"{accessibility} ");

		if (isStatic) declaration.Append("static ");

		declaration.Append($"{returnType} {name}(");

		if (parameters is { Length: > 0 }) declaration.Append(string.Join(", ", parameters));

		declaration.Append(")");
		_writer.WriteLine(declaration.ToString());

		using (_writer.BeginScope())
		{
			bodyWriter?.Invoke(_writer);
		}

		return this;
	}

	/// <summary>
	/// Adds a property declaration to the current class.
	/// </summary>
	/// <param name="name">The property name.</param>
	/// <param name="type">The type of the property.</param>
	/// <param name="accessibility">Accessibility modifier.</param>
	/// <param name="hasGetter">Whether to include a getter.</param>
	/// <param name="hasSetter">Whether to include a setter.</param>
	/// <param name="setterAccessibility">Optionally specify accessor modifier for setter.</param>
	/// <returns>This <see cref="CSharpCodeBuilder"/> instance for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if not in a class definition.</exception>
	/// <exception cref="ArgumentException">Thrown if neither accessor is present.</exception>
	public CSharpCodeBuilder AddProperty(
		string  name,
		string  type,
		string  accessibility       = "public",
		bool    hasGetter           = true,
		bool    hasSetter           = true,
		string? setterAccessibility = null
	)
	{
		if (!_isInClass) throw new InvalidOperationException("Must be in a class to add a property");

		var declaration = $"{accessibility} {type} {name}";

		if (!hasGetter && !hasSetter) throw new ArgumentException("Property must have at least a getter or setter");

		_writer.Write(declaration);

		using (_writer.BeginScope(" {"))
		{
			if (hasGetter) _writer.WriteLine("get;");

			if (hasSetter) _writer.WriteLine($"{setterAccessibility ?? ""} set;".TrimStart());
		}

		return this;
	}

	/// <summary>
	/// Adds a using directive for the specified namespace.
	/// </summary>
	/// <param name="nameSpace">The namespace to add as a using directive.</param>
	/// <returns>This <see cref="CSharpCodeBuilder"/> instance for chaining.</returns>
	public CSharpCodeBuilder AddUsing(string nameSpace)
	{
		_writer.AddUsing(nameSpace);
		return this;
	}

	/// <summary>
	/// Begins a class definition, writing its declaration and opening brace.
	/// </summary>
	/// <param name="className">The class name.</param>
	/// <param name="accessibility">The accessibility of the class.</param>
	/// <param name="isStatic">Whether the class is static.</param>
	/// <param name="baseClass">Optional base class name.</param>
	/// <param name="interfaces">List of implemented interfaces.</param>
	/// <returns>This <see cref="CSharpCodeBuilder"/> instance for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if already in a class definition.</exception>
	public CSharpCodeBuilder BeginClass(
		string    className,
		string    accessibility = "public",
		bool      isStatic      = false,
		string?   baseClass     = null,
		string[]? interfaces    = null
	)
	{
		if (_isInClass) throw new InvalidOperationException("Already in a class definition");

		var declaration = new StringBuilder();
		declaration.Append($"{accessibility} ");
		if (isStatic) declaration.Append("static ");

		declaration.Append($"class {className}");
		if (baseClass != null) declaration.Append($" : {baseClass}");

		if (interfaces is { Length: > 0 })
		{
			declaration.Append(baseClass == null ? " : " : ", ");
			declaration.Append(string.Join(", ", interfaces));
		}

		_writer.WriteLine(declaration.ToString());
		_writer.BeginScope();
		_isInClass = true;
		return this;
	}

	/// <summary>
	/// Begins a namespace block.
	/// </summary>
	/// <param name="nameSpace">The name of the namespace.</param>
	/// <returns>This <see cref="CSharpCodeBuilder"/> instance for chaining.</returns>
	public CSharpCodeBuilder BeginNamespace(string nameSpace)
	{
		_namespaceStack.Push(nameSpace);
		_writer.WriteLine($"namespace {nameSpace}");
		_writer.BeginScope();
		return this;
	}

	/// <summary>
	/// Ends the current class definition.
	/// </summary>
	/// <returns>This <see cref="CSharpCodeBuilder"/> instance for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if not in a class definition.</exception>
	public CSharpCodeBuilder EndClass()
	{
		if (!_isInClass) throw new InvalidOperationException("Not in a class definition");

		_writer.EndScope();
		_isInClass = false;
		return this;
	}

	/// <summary>
	/// Ends the most recent namespace block.
	/// </summary>
	/// <returns>This <see cref="CSharpCodeBuilder"/> instance for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no namespace is being defined.</exception>
	public CSharpCodeBuilder EndNamespace()
	{
		if (_namespaceStack.Count == 0) throw new InvalidOperationException("No namespace to end");

		_namespaceStack.Pop();
		_writer.EndScope();
		return this;
	}

	/// <summary>
	/// Generates the code accumulated in the builder as a string.
	/// </summary>
	/// <returns>The generated code as a string.</returns>
	/// <exception cref="InvalidOperationException">Thrown if there are unclosed namespaces or classes.</exception>
	public string Generate()
	{
		if (_namespaceStack.Count > 0) throw new InvalidOperationException("Unclosed namespace definitions");

		if (_isInClass) throw new InvalidOperationException("Unclosed class definition");

		return _writer.ToString();
	}
}

/// <summary>
/// Handles file output for generated C# code using the <see cref="CSharpCodeBuilder"/>.
/// </summary>
public class CSharpFileGenerator
{
	private readonly CSharpCodeBuilder _builder;
	private readonly string            _outputPath;

	/// <summary>
	/// Constructs a new file generator for an output path.
	/// </summary>
	/// <param name="outputPath">The file path where generated code will be written.</param>
	public CSharpFileGenerator(string outputPath)
	{
		_outputPath = outputPath;
		_builder    = new();
	}

	/// <summary>
	/// Returns the underlying <see cref="CSharpCodeBuilder"/> to be used for code construction.
	/// </summary>
	public CSharpCodeBuilder GetBuilder() =>
		_builder;

	/// <summary>
	/// Generates the C# file and writes it to disk at the output path.
	/// </summary>
	public void Generate()
	{
		string code = _builder.Generate();
		File.WriteAllText(_outputPath, code);
	}
}
