using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Bezoro.ECS.SourceGen.Generators;

[Generator]
public sealed class QuerySourceGenerator : IIncrementalGenerator
{
	private const string QUERY_ATTRIBUTE_NAME = "Bezoro.ECS.Attributes.QueryAttribute";
	private static readonly DiagnosticDescriptor UnsupportedQueryAttributeDescriptor = new(
		"BECSG001",
		"Unsupported query attribute",
		"Query attribute '{0}' is not supported by generated compiled-query specs and will be ignored",
		"Usage",
		DiagnosticSeverity.Warning,
		true
	);

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var queryModels = context.SyntaxProvider.ForAttributeWithMetadataName(
									 QUERY_ATTRIBUTE_NAME,
									 static (node, _) => node is StructDeclarationSyntax,
									 static (syntaxContext, _) =>
										 CreateModel((INamedTypeSymbol)syntaxContext.TargetSymbol)
								 )
								 .Where(static model => model is { })
								 .Select(static (model, _) => model!);

		context.RegisterSourceOutput(
			queryModels, static (productionContext, model) =>
			{
				for (var i = 0; i < model.UnsupportedAttributeNames.Length; i++)
				{
					productionContext.ReportDiagnostic(
						Diagnostic.Create(
							UnsupportedQueryAttributeDescriptor,
							model.DeclarationLocation,
							model.UnsupportedAttributeNames[i]
						)
					);
				}

				if (!model.IsPartial || model.IsNested)
					return;

				string source = BuildSource(model);
				productionContext.AddSource(model.HintName, SourceText.From(source, Encoding.UTF8));
			}
		);
	}

	private static bool IsPartial(INamedTypeSymbol symbol)
	{
		for (var i = 0; i < symbol.DeclaringSyntaxReferences.Length; i++)
		{
			if (symbol.DeclaringSyntaxReferences[i].GetSyntax() is StructDeclarationSyntax structSyntax)
				for (var m = 0; m < structSyntax.Modifiers.Count; m++)
				{
					if (structSyntax.Modifiers[m].IsKind(SyntaxKind.PartialKeyword))
						return true;
				}
		}

		return false;
	}

	private static QueryModel? CreateModel(INamedTypeSymbol symbol)
	{
		if (symbol.TypeKind != TypeKind.Struct)
			return null;

		var all         = new HashSet<string>(StringComparer.Ordinal);
		var none        = new HashSet<string>(StringComparer.Ordinal);
		var any         = new HashSet<string>(StringComparer.Ordinal);
		var optional    = new HashSet<string>(StringComparer.Ordinal);
		var added       = new HashSet<string>(StringComparer.Ordinal);
		var changed     = new HashSet<string>(StringComparer.Ordinal);
		var unsupported = new HashSet<string>(StringComparer.Ordinal);

		foreach (var attribute in symbol.GetAttributes())
		{
			var attributeClass = attribute.AttributeClass;
			if (attributeClass is null) continue;

			if (attributeClass.ContainingNamespace.ToDisplayString() != "Bezoro.ECS.Attributes")
				continue;

			if (attributeClass.Name == "AllAttribute")
			{
				if (attributeClass.TypeArguments.Length == 1)
					all.Add(ToFullyQualified(attributeClass.TypeArguments[0]));
			}
			else if (attributeClass.Name == "NoneAttribute")
			{
				if (attributeClass.TypeArguments.Length == 1)
					none.Add(ToFullyQualified(attributeClass.TypeArguments[0]));
			}
			else if (attributeClass.Name == "AnyAttribute")
			{
				if (attributeClass.TypeArguments.Length == 2)
				{
					any.Add(ToFullyQualified(attributeClass.TypeArguments[0]));
					any.Add(ToFullyQualified(attributeClass.TypeArguments[1]));
				}
			}
			else if (attributeClass.Name == "OptionalAttribute")
			{
				if (attributeClass.TypeArguments.Length == 1)
					optional.Add(ToFullyQualified(attributeClass.TypeArguments[0]));
			}
			else if (attributeClass.Name == "ChangedAttribute")
			{
				if (attributeClass.TypeArguments.Length == 1)
					changed.Add(ToFullyQualified(attributeClass.TypeArguments[0]));
			}
			else if (attributeClass.Name == "AddedAttribute")
			{
				if (attributeClass.TypeArguments.Length == 1)
					added.Add(ToFullyQualified(attributeClass.TypeArguments[0]));
			}
			else if (attributeClass.Name != "QueryAttribute")
			{
				unsupported.Add(attributeClass.Name);
			}
		}

		string typeNamespace = symbol.ContainingNamespace.IsGlobalNamespace
								   ? string.Empty
								   : symbol.ContainingNamespace.ToDisplayString();
		string hintName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
								.Replace("global::", string.Empty)
								.Replace('<',        '_')
								.Replace('>',        '_')
								.Replace('.',        '_') +
						  ".CompiledQuery.g.cs";

		return new(
			typeNamespace,
			symbol.Name,
			hintName,
			all.ToArray(),
			none.ToArray(),
			any.ToArray(),
			optional.ToArray(),
			added.ToArray(),
			changed.ToArray(),
			unsupported.OrderBy(static name => name, StringComparer.Ordinal).ToArray(),
			symbol.Locations.FirstOrDefault() ?? Location.None,
			IsPartial(symbol),
			symbol.IsReadOnly,
			symbol.ContainingType is { }
		);
	}

	private static string BuildSource(QueryModel model)
	{
		var builder = new StringBuilder();
		builder.AppendLine("// <auto-generated />");

		if (!string.IsNullOrEmpty(model.Namespace))
		{
			builder.Append("namespace ").Append(model.Namespace).AppendLine(";");
			builder.AppendLine();
		}

		builder.Append(model.IsReadOnly ? "readonly partial struct " : "partial struct ")
			   .Append(model.TypeName)
			   .AppendLine(" : global::Bezoro.ECS.Abstractions.ICompiledQuerySpec");
		builder.AppendLine("{");
		builder.AppendLine(
			"    void global::Bezoro.ECS.Abstractions.ICompiledQuerySpec.Build(ref global::Bezoro.ECS.Types.QueryBuilder builder)"
		);
		builder.AppendLine("    {");

		var sortedAll = model.All.OrderBy(static t => t, StringComparer.Ordinal).ToArray();
		for (var i = 0; i < sortedAll.Length; i++)
			builder.Append("        builder.All<").Append(sortedAll[i]).AppendLine(">();");

		var sortedNone = model.None.OrderBy(static t => t, StringComparer.Ordinal).ToArray();
		for (var i = 0; i < sortedNone.Length; i++)
			builder.Append("        builder.None<").Append(sortedNone[i]).AppendLine(">();");

		var sortedAny = model.Any.OrderBy(static t => t, StringComparer.Ordinal).ToArray();
		for (var i = 0; i < sortedAny.Length; i++)
			builder.Append("        builder.Any<").Append(sortedAny[i]).AppendLine(">();");

		var sortedOptional = model.Optional.OrderBy(static t => t, StringComparer.Ordinal).ToArray();
		for (var i = 0; i < sortedOptional.Length; i++)
			builder.Append("        builder.Optional<").Append(sortedOptional[i]).AppendLine(">();");

		var sortedChanged = model.Changed.OrderBy(static t => t, StringComparer.Ordinal).ToArray();
		for (var i = 0; i < sortedChanged.Length; i++)
			builder.Append("        builder.Changed<").Append(sortedChanged[i]).AppendLine(">();");

		var sortedAdded = model.Added.OrderBy(static t => t, StringComparer.Ordinal).ToArray();
		for (var i = 0; i < sortedAdded.Length; i++)
			builder.Append("        builder.Added<").Append(sortedAdded[i]).AppendLine(">();");

		builder.AppendLine("    }");
		builder.AppendLine("}");
		return builder.ToString();
	}

	private static string ToFullyQualified(ITypeSymbol symbol) =>
		symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

	private sealed class QueryModel(
		string      typeNamespace,
		string      typeName,
		string      hintName,
		string[]    all,
		string[]    none,
		string[]    any,
		string[]    optional,
		string[]    added,
		string[]    changed,
		string[]    unsupportedAttributeNames,
		Location    declarationLocation,
		bool        isPartial,
		bool        isReadOnly,
		bool        isNested
	)
	{
		public bool IsNested   { get; } = isNested;
		public bool IsPartial  { get; } = isPartial;
		public bool IsReadOnly { get; } = isReadOnly;

		public string[] All                { get; } = all;
		public string[] Added              { get; } = added;
		public string[] Any                { get; } = any;
		public string[] Changed            { get; } = changed;
		public string[] UnsupportedAttributeNames { get; } = unsupportedAttributeNames;
		public Location DeclarationLocation { get; } = declarationLocation;
		public string   HintName           { get; } = hintName;
		public string[] None               { get; } = none;
		public string[] Optional           { get; } = optional;
		public string   TypeName           { get; } = typeName;
		public string   Namespace          { get; } = typeNamespace;
	}
}
