using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AotAnnotationInventory;

/// <summary>Extracts annotation spellings and declaration identities.</summary>
internal static class AnnotationSyntax
{
	/// <summary>Reads the explicit source declaration without claiming semantic binding.</summary>
	/// <exception cref="InvalidDataException"></exception>
	internal static string? Annotation(NameSyntax name, SyntaxNode context,
		IEnumerable<UsingDirectiveSyntax> globalAliases, HashSet<string> shadows)
	{
		var value = Text(name).Replace("global::", string.Empty, StringComparison.Ordinal);

		// Resolve lexical using aliases, including aliases for the attribute itself or its namespace.
		var aliases = context.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
			.SelectMany(n => n.Usings).Concat(context.SyntaxTree.GetCompilationUnitRoot().Usings)
			.Where(u => u.Alias is not null && u.Name is not null).Concat(globalAliases).Distinct();
		foreach (var group in aliases.GroupBy(a => a.Alias!.Name.Identifier.ValueText, StringComparer.Ordinal))
		{
			var prefix = group.Key;
			if (group.Any(a => a.Name is null))
			{
				throw new InvalidDataException($"Unsupported non-name alias {prefix}; semantic review required.");
			}

			var targets = group.Select(a => Text(a.Name!)).Distinct(StringComparer.Ordinal).ToArray();
			if (targets.Length != 1)
			{
				throw new InvalidDataException($"Conflicting alias {prefix} in {context.SyntaxTree.FilePath}");
			}

			var target = targets[0];
			if (string.Equals(value, prefix, StringComparison.Ordinal))
			{
				value = target;
			}
			else if (value.StartsWith(prefix + ".", StringComparison.Ordinal) || value.StartsWith(prefix + "::", StringComparison.Ordinal))
			{
				value = target + "." + value[(prefix.Length + (value[prefix.Length] == ':' ? 2 : 1))..];
			}
		}

		value = value.Replace("global::", string.Empty, StringComparison.Ordinal);
		var shortName = value.StartsWith("System.Diagnostics.CodeAnalysis.", StringComparison.Ordinal)
			? value["System.Diagnostics.CodeAnalysis.".Length..] : value;
		if (shadows.Contains(shortName) || shadows.Contains(shortName + "Attribute"))
		{
			throw new InvalidDataException($"Attribute shadow requires semantic review: {value}");
		}

		if (shortName.Contains("RequiresDynamicCode", StringComparison.Ordinal) || shortName.Contains("RequiresUnreferencedCode", StringComparison.Ordinal))
		{
			if (shortName is not ("RequiresDynamicCode" or "RequiresDynamicCodeAttribute" or "RequiresUnreferencedCode" or "RequiresUnreferencedCodeAttribute"))
			{
				throw new InvalidDataException($"Unresolved or conflicting annotation: {value}");
			}
		}

		return shortName switch
		{
			"RequiresDynamicCode" or "RequiresDynamicCodeAttribute" => "RequiresDynamicCode",
			"RequiresUnreferencedCode" or "RequiresUnreferencedCodeAttribute" => "RequiresUnreferencedCode",
			_ => null,
		};
	}

	/// <summary>Reads the explicit source declaration without claiming semantic binding.</summary>
	internal static SyntaxList<AttributeListSyntax> Attributes(SyntaxNode node) => node switch
	{
		MemberDeclarationSyntax m => m.AttributeLists,
		AccessorDeclarationSyntax a => a.AttributeLists,
		_ => default,
	};

	/// <summary>Reads the explicit source declaration without claiming semantic binding.</summary>
	internal static bool Visible(SyntaxNode node)
	{
		if (node is BaseNamespaceDeclarationSyntax)
		{
			return false;
		}

		if (node.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any(t => !OwnVisibility(t)))
		{
			return false;
		}

		if (node is AccessorDeclarationSyntax accessor)
		{
			return accessor.Parent?.Parent is MemberDeclarationSyntax owner && Visible(owner)
				&& (!accessor.Modifiers.Any() || OwnVisibility(accessor));
		}

		return OwnVisibility(node);
	}

	/// <summary>Reads the explicit source declaration without claiming semantic binding.</summary>
	internal static string Container(SyntaxNode node)
	{
		var parts = node.Ancestors().Reverse().Select(n => n switch
		{
			BaseNamespaceDeclarationSyntax ns => Text(ns.Name),
			TypeDeclarationSyntax type => type.Identifier.ValueText + Arity(type.TypeParameterList),
			EnumDeclarationSyntax type => type.Identifier.ValueText,
			_ => null,
		}).Where(n => n is not null);
		return string.Join('.', parts) is { Length: > 0 } value ? value + "." : string.Empty;
	}

	/// <summary>Reads the explicit source declaration without claiming semantic binding.</summary>
	internal static IEnumerable<string> Signatures(SyntaxNode node)
	{
		var signature = node switch
		{
			MethodDeclarationSyntax m => m.Identifier.ValueText + Arity(m.TypeParameterList) + Parameters(m.ParameterList.Parameters),
			ConstructorDeclarationSyntax c => "#ctor" + Parameters(c.ParameterList.Parameters),
			OperatorDeclarationSyntax o => "operator " + o.CheckedKeyword.Text + o.OperatorToken.Text + Parameters(o.ParameterList.Parameters),
			ConversionOperatorDeclarationSyntax c => c.ImplicitOrExplicitKeyword.Text + " operator " + c.CheckedKeyword.Text + Text(c.Type) + Parameters(c.ParameterList.Parameters),
			IndexerDeclarationSyntax i => "this" + Parameters(i.ParameterList.Parameters),
			PropertyDeclarationSyntax p => p.Identifier.ValueText,
			EventDeclarationSyntax e => e.Identifier.ValueText,
			TypeDeclarationSyntax t => t.Identifier.ValueText + Arity(t.TypeParameterList),
			EnumDeclarationSyntax e => e.Identifier.ValueText,
			EnumMemberDeclarationSyntax e => e.Identifier.ValueText,
			DelegateDeclarationSyntax d => d.Identifier.ValueText + Arity(d.TypeParameterList) + Parameters(d.ParameterList.Parameters),
			AccessorDeclarationSyntax a => Signatures(a.Parent!.Parent!).Single() + "." + a.Keyword.Text,
			_ => null,
		};
		if (signature is not null)
		{
			yield return signature;
		}

		if (node is BaseFieldDeclarationSyntax field)
		{
			foreach (var variable in field.Declaration.Variables)
			{
				yield return variable.Identifier.ValueText;
			}
		}
	}

	private static string Text(SyntaxNode node) => string.Concat(node.DescendantTokens().Select(t => t.Text));

	private static bool OwnVisibility(SyntaxNode node)
	{
		var modifiers = node switch
		{
			MemberDeclarationSyntax m => m.Modifiers,
			AccessorDeclarationSyntax a => a.Modifiers,
			_ => default,
		};
		if (modifiers.Any(SyntaxKind.PrivateKeyword) || modifiers.Any(SyntaxKind.FileKeyword))
		{
			return false;
		}

		if (node is MethodDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
			or PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
			or IndexerDeclarationSyntax { ExplicitInterfaceSpecifier: not null }
			or EventDeclarationSyntax { ExplicitInterfaceSpecifier: not null })
		{
			return false;
		}

		if (modifiers.Any(SyntaxKind.PublicKeyword) || modifiers.Any(SyntaxKind.ProtectedKeyword))
		{
			return true;
		}

		return node.Parent is EnumDeclarationSyntax ||
			(node.Parent is InterfaceDeclarationSyntax && !modifiers.Any(SyntaxKind.InternalKeyword));
	}

	private static string Arity(TypeParameterListSyntax? list) => list is null ? string.Empty : "`" + list.Parameters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

	private static string Parameters(SeparatedSyntaxList<ParameterSyntax> parameters) => "(" + string.Join(',', parameters.Select(p =>
		string.Join(' ', p.Modifiers.Select(m => m.Text).Append(p.Type is null ? "?" : Text(p.Type))))) + ")";
}
