using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Bezoro.Core
{
	public class CodeWriter
	{
		private const    string          _INDENT_STRING = "    "; // 4 spaces
		private          bool            _isNewLine     = true;
		private readonly HashSet<string> _usings        = new();

		private int _indentLevel;

		private readonly StringBuilder _builder = new();

		public override string ToString()
		{
			var finalBuilder = new StringBuilder();

			// Write file header
			finalBuilder.AppendLine("// This file is auto-generated. Do not modify.");
			finalBuilder.AppendLine();

			// Write usings
			foreach (var @using in _usings.OrderBy(x => x))
			{
				finalBuilder.AppendLine($"using {@using};");
			}

			if (_usings.Any()) finalBuilder.AppendLine();

			// Write main content
			finalBuilder.Append(_builder.ToString());
			return finalBuilder.ToString();
		}

		public CodeWriter AddUsing(string nameSpace)
		{
			_usings.Add(nameSpace);
			return this;
		}

		public CodeWriter AddUsingRange(IEnumerable<string> namespaces)
		{
			foreach (var @namespace in namespaces)
			{
				AddUsing(@namespace);
			}

			return this;
		}

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

		public IDisposable BeginScope(string declaration = null)
		{
			if (declaration != null) WriteLine(declaration);
			WriteLine("{");
			_indentLevel++;
			return new ScopeGuard(this);
		}

		public void EndScope(string closer = "}")
		{
			_indentLevel--;
			WriteLine(closer);
		}

		private void WriteIndent()
		{
			for (var i = 0 ; i < _indentLevel ; i++) _builder.Append(_INDENT_STRING);
		}

		private class ScopeGuard : IDisposable
		{
			public ScopeGuard(CodeWriter writer)
			{
				_writer = writer;
			}

			private readonly CodeWriter _writer;

			public void Dispose() =>
				_writer.EndScope();
		}
	}

	public class CSharpCodeBuilder
	{
		private          bool          _isInClass;
		private readonly CodeWriter    _writer         = new();
		private readonly Stack<string> _namespaceStack = new();

		public CSharpCodeBuilder AddField(
			string name,
			string type,
			string accessibility = "private",
			bool isReadonly = false,
			string initialValue = null
		)
		{
			if (!_isInClass)
				throw new InvalidOperationException("Must be in a class to add a field");

			var declaration = new StringBuilder();
			declaration.Append($"{accessibility} ");

			if (isReadonly) declaration.Append("readonly ");

			declaration.Append($"{type} {name}");

			if (initialValue != null) declaration.Append($" = {initialValue}");

			declaration.Append(";");

			_writer.WriteLine(declaration.ToString());
			return this;
		}

		public CSharpCodeBuilder AddMethod(
			string name,
			string returnType = "void",
			string accessibility = "public",
			bool isStatic = false,
			string[] parameters = null,
			Action<CodeWriter> bodyWriter = null,
			string[] attributes = null
		)
		{
			if (!_isInClass) throw new InvalidOperationException("Must be in a class to add a method");

			// Write attributes
			if (attributes != null)
			{
				foreach (var attribute in attributes)
				{
					_writer.WriteLine($"[{attribute}]");
				}
			}

			var declaration = new StringBuilder();
			declaration.Append($"{accessibility} ");

			if (isStatic) declaration.Append("static ");

			declaration.Append($"{returnType} {name}(");

			if (parameters is { Length: > 0 }) declaration.Append(string.Join(", ", parameters));

			declaration.Append(")");
			_writer.WriteLine(declaration.ToString());

			using ( _writer.BeginScope() ) bodyWriter?.Invoke(_writer);

			return this;
		}

		public CSharpCodeBuilder AddProperty(
			string name,
			string type,
			string accessibility = "public",
			bool hasGetter = true,
			bool hasSetter = true,
			string setterAccessibility = null
		)
		{
			if (!_isInClass) throw new InvalidOperationException("Must be in a class to add a property");

			var declaration = $"{accessibility} {type} {name}";

			if (!hasGetter && !hasSetter)
				throw new ArgumentException("Property must have at least a getter or setter");

			_writer.Write(declaration);

			using ( _writer.BeginScope(" {") )
			{
				if (hasGetter) _writer.WriteLine("get;");
				if (hasSetter) _writer.WriteLine($"{setterAccessibility ?? ""} set;".TrimStart());
			}

			return this;
		}

		public CSharpCodeBuilder AddUsing(string nameSpace)
		{
			_writer.AddUsing(nameSpace);
			return this;
		}

		public CSharpCodeBuilder BeginClass(
			string className,
			string accessibility = "public",
			bool isStatic = false,
			string baseClass = null,
			string[] interfaces = null
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

		public CSharpCodeBuilder BeginNamespace(string nameSpace)
		{
			_namespaceStack.Push(nameSpace);
			_writer.WriteLine($"namespace {nameSpace}");
			_writer.BeginScope();
			return this;
		}

		public CSharpCodeBuilder EndClass()
		{
			if (!_isInClass) throw new InvalidOperationException("Not in a class definition");

			_writer.EndScope();
			_isInClass = false;
			return this;
		}

		public CSharpCodeBuilder EndNamespace()
		{
			if (_namespaceStack.Count == 0)
				throw new InvalidOperationException("No namespace to end");

			_namespaceStack.Pop();
			_writer.EndScope();
			return this;
		}

		public string Generate()
		{
			if (_namespaceStack.Count > 0)
				throw new InvalidOperationException("Unclosed namespace definitions");

			if (_isInClass) throw new InvalidOperationException("Unclosed class definition");

			return _writer.ToString();
		}
	}

	public class CSharpFileGenerator
	{
		public CSharpFileGenerator(string outputPath)
		{
			_outputPath = outputPath;
			_builder    = new();
		}

		private readonly CSharpCodeBuilder _builder;
		private readonly string            _outputPath;

		public CSharpCodeBuilder GetBuilder() =>
			_builder;

		public void Generate()
		{
			var code = _builder.Generate();
			File.WriteAllText(_outputPath, code);
		}
	}
}
