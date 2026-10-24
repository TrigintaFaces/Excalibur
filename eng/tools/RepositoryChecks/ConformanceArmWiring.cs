// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Measures static references to published conformance arms from discovered test entry points.</summary>
/// <remarks>Reference coverage does not prove runtime execution. Behavioral suites remain required.</remarks>
internal static class ConformanceArmWiring
{
	private const string PartialMarker = "conformance-partial-suite: full coverage in ";

	/// <summary>Collects suites by resolved inheritance, including inherited test wrappers and partial declarations.</summary>
	internal static IReadOnlyList<ArmSuite> Analyze(Compilation compilation, IAssemblySymbol? published)
	{
		if (published is null)
		{
			return [];
		}

		var suites = new List<ArmSuite>();
		foreach (var suite in ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace)
			.Where(type => type.TypeKind == TypeKind.Class && !type.IsAbstract && !type.IsGenericType))
		{
			var parents = Ancestors(suite).ToArray();
			var kit = parents.FirstOrDefault(type => type.IsAbstract &&
				SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, published) &&
				type.Name.EndsWith("TestKit", StringComparison.Ordinal));
			if (kit is null)
			{
				continue;
			}

			var arms = Ancestors(kit, includeSelf: true).Where(type => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, published))
				.SelectMany(type => type.GetMembers().OfType<IMethodSymbol>()).Where(IsArm)
				.Distinct<IMethodSymbol>(SymbolEqualityComparer.Default).ToArray();
			if (arms.Length == 0)
			{
				continue;
			}

			var references = CollectReferences(compilation, published, suite);
			var entryPoints = EffectiveMethods(suite).Count(IsTest);
			var missing = arms.Where(arm => !references.Contains(MethodIdentity(arm))).Select(MethodIdentity).Order(StringComparer.Ordinal).ToArray();
			suites.Add(new ArmSuite(TypeIdentity(suite), suite.Name, TypeIdentity(kit), arms.Length, entryPoints, missing, PartialSibling(suite), IsHelper(suite)));
		}

		return suites;
	}

	/// <summary>Checks exact sibling deferrals after every project's facts have been collected.</summary>
	/// <exception cref="InvalidOperationException">No arms or suites were measured, or a duplicate suite changed between compilations.</exception>
	internal static string[] Evaluate(IEnumerable<ArmSuite> measured)
	{
		var groups = measured.Where(suite => !suite.IsHelper).GroupBy(suite => suite.Identity, StringComparer.Ordinal).ToArray();
		if (groups.Length == 0 || !groups.Any(group => group.First().Arms > 0))
		{
			throw new InvalidOperationException("No conformance suites or arms were measured.");
		}

		foreach (var group in groups)
		{
			var first = group.First();
			if (group.Any(candidate => !string.Equals(candidate.Kit, first.Kit, StringComparison.Ordinal) || candidate.Arms != first.Arms ||
				candidate.EntryPoints != first.EntryPoints || !string.Equals(candidate.PartialSibling, first.PartialSibling, StringComparison.Ordinal) ||
				!candidate.Missing.SequenceEqual(first.Missing, StringComparer.Ordinal)))
			{
				throw new InvalidOperationException("Inconsistent conformance suite across compilations: " + group.Key);
			}
		}

		var suites = groups.Select(group => group.First()).ToArray();
		var failures = new List<string>();
		foreach (var suite in suites.Where(suite => suite.Missing.Length > 0))
		{
			if (suite.PartialSibling is null)
			{
				failures.Add(suite.Identity + " has no discovered test reference to: " + string.Join(", ", suite.Missing));
				continue;
			}

			var siblings = suites.Where(candidate => string.Equals(candidate.Name, suite.PartialSibling, StringComparison.Ordinal)).ToArray();
			if (siblings.Length != 1 || !string.Equals(siblings[0].Kit, suite.Kit, StringComparison.Ordinal) || siblings[0].Missing.Length != 0)
			{
				failures.Add(suite.Identity + " requires one fully wired sibling of the same constructed kit: " + suite.PartialSibling);
			}
		}

		return failures.Order(StringComparer.Ordinal).ToArray();
	}

	/// <summary>Identifies a published virtual conformance arm by its resolved signature.</summary>
	internal static bool IsArm(IMethodSymbol method) => !method.IsStatic && !method.IsSealed &&
		(method.IsVirtual || method.IsOverride || method.IsAbstract) && method.Parameters.Length == 0 && method.Name.Contains('_', StringComparison.Ordinal) &&
		method.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal &&
		(method.ReturnsVoid || string.Equals(method.ReturnType.ToDisplayString(), "System.Threading.Tasks.Task", StringComparison.Ordinal));

	/// <summary>Enumerates the resolved class inheritance chain.</summary>
	internal static IEnumerable<INamedTypeSymbol> Ancestors(INamedTypeSymbol type, bool includeSelf = false)
	{
		for (var current = includeSelf ? type : type.BaseType; current is not null; current = current.BaseType)
		{
			yield return current;
		}
	}

	/// <summary>Finds the owning source compilation, including project references.</summary>
	/// <exception cref="InvalidOperationException">The declaration has no owning source compilation.</exception>
	internal static SemanticModel ModelFor(Compilation compilation, SyntaxTree tree)
	{
		var pending = new Stack<Compilation>();
		var visited = new HashSet<Compilation>();
		pending.Push(compilation);
		while (pending.TryPop(out var current))
		{
			if (!visited.Add(current))
			{
				continue;
			}

			if (current.ContainsSyntaxTree(tree))
			{
				return current.GetSemanticModel(tree);
			}

			foreach (var reference in current.References.OfType<CompilationReference>())
			{
				pending.Push(reference.Compilation);
			}
		}

		throw new InvalidOperationException("No source compilation for conformance wrapper " + tree.FilePath);
	}

	/// <summary>Recognizes resolved xUnit test attributes, including inherited custom facts.</summary>
	internal static bool IsTest(IMethodSymbol method)
	{
		for (var current = method; current is not null; current = current.OverriddenMethod)
		{
			if (current.GetAttributes().Any(attribute => attribute.AttributeClass is { } type &&
				Ancestors(type, includeSelf: true).Any(parent => string.Equals(parent.ToDisplayString(), "Xunit.FactAttribute", StringComparison.Ordinal)) &&
				(SymbolEqualityComparer.Default.Equals(current, method) || IsInheritedAttribute(type))))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Enumerates effective instance methods without borrowing hidden or overridden base bodies.</summary>
	internal static IEnumerable<IMethodSymbol> EffectiveMethods(INamedTypeSymbol suite)
	{
		var hidden = new HashSet<string>(StringComparer.Ordinal);
		foreach (var type in Ancestors(suite, includeSelf: true))
		{
			foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(method => method.MethodKind == MethodKind.Ordinary && !method.IsStatic))
			{
				var signature = method.Name + "`" + method.Arity.ToString(System.Globalization.CultureInfo.InvariantCulture) + "(" +
					string.Join(',', method.Parameters.Select(parameter => parameter.RefKind + ":" + parameter.Type.ToDisplayString())) + ")";
				if (hidden.Add(signature))
				{
					yield return method;
				}
			}
		}
	}

	private static HashSet<string> CollectReferences(Compilation compilation, IAssemblySymbol published, INamedTypeSymbol suite)
	{
		var references = new HashSet<string>(StringComparer.Ordinal);
		var visited = new HashSet<string>(StringComparer.Ordinal);
		var methods = EffectiveMethods(suite).ToArray();
		var queue = new Queue<IMethodSymbol>(methods.Where(IsTest));
		var family = Ancestors(suite, includeSelf: true).Select(type => TypeIdentity(type.OriginalDefinition)).ToHashSet(StringComparer.Ordinal);
		while (queue.TryDequeue(out var method))
		{
			if (!visited.Add(MethodIdentity(method)))
			{
				continue;
			}

			AddContractReferences(references, method);

			foreach (var declaration in method.DeclaringSyntaxReferences)
			{
				var syntax = declaration.GetSyntax();
				var model = ModelFor(compilation, syntax.SyntaxTree);
				foreach (var node in syntax.DescendantNodesAndSelf())
				{
					var referenced = model.GetOperation(node) switch
					{
						IInvocationOperation invocation => invocation.TargetMethod,
						IMethodReferenceOperation reference => reference.Method,
						_ => null,
					};
					if (referenced is null)
					{
						continue;
					}

					if (node is not (InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } } or
						MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }))
					{
						referenced = ResolveOverride(referenced, methods);
					}

					AddContractReferences(references, referenced);

					// Calls into helpers on this suite's inheritance chain remain part of its static wiring.
					// References alone (including delegate expressions) are not a claim that a helper runs.
					if (family.Contains(TypeIdentity(referenced.ContainingType.OriginalDefinition)) &&
						!SymbolEqualityComparer.Default.Equals(referenced.ContainingAssembly, published))
					{
						queue.Enqueue(referenced);
					}
				}
			}
		}

		return references;
	}

	private static bool IsInheritedAttribute(INamedTypeSymbol type)
	{
		var usage = Ancestors(type, includeSelf: true).SelectMany(parent => parent.GetAttributes()).FirstOrDefault(attribute =>
			string.Equals(attribute.AttributeClass?.ToDisplayString(), "System.AttributeUsageAttribute", StringComparison.Ordinal));
		return usage?.NamedArguments.Any(argument =>
			string.Equals(argument.Key, "Inherited", StringComparison.Ordinal) && argument.Value.Value is false) != true;
	}

	private static void AddContractReferences(HashSet<string> references, IMethodSymbol method)
	{
		for (var current = method; current is not null; current = current.OverriddenMethod)
		{
			references.Add(MethodIdentity(current));
		}
	}

	private static bool IsHelper(INamedTypeSymbol suite)
	{
		for (var current = suite; current is not null; current = current.ContainingType)
		{
			if (current.IsFileLocal || current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
			{
				return true;
			}
		}

		return false;
	}

	private static IMethodSymbol ResolveOverride(IMethodSymbol target, IMethodSymbol[] methods)
	{
		foreach (var method in methods)
		{
			for (var parent = method.OverriddenMethod; parent is not null; parent = parent.OverriddenMethod)
			{
				if (string.Equals(MethodIdentity(parent), MethodIdentity(target), StringComparison.Ordinal))
				{
					return method;
				}
			}
		}

		return target;
	}

	private static string? PartialSibling(INamedTypeSymbol suite)
	{
		var siblings = new HashSet<string>(StringComparer.Ordinal);
		foreach (var declaration in suite.DeclaringSyntaxReferences)
		{
			var syntax = declaration.GetSyntax();

			// Comments before attributes and between attributes and the declaration belong to this type only.
			var prefixEnd = syntax is TypeDeclarationSyntax type ? type.Identifier.SpanStart : syntax.SpanStart;
			foreach (var trivia in syntax.DescendantTrivia(descendIntoTrivia: true).Where(trivia => trivia.SpanStart < prefixEnd &&
				(trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))))
			{
				var text = trivia.ToString();
				var start = text.IndexOf(PartialMarker, StringComparison.Ordinal);
				if (start >= 0)
				{
					var name = text[(start + PartialMarker.Length)..].Trim().TrimEnd('*', '/').Trim();
					if (!SyntaxFacts.IsValidIdentifier(name))
					{
						throw new InvalidOperationException("Invalid partial-suite sibling on " + TypeIdentity(suite));
					}

					siblings.Add(name);
				}
			}
		}

		return siblings.Count switch
		{
			0 => null,
			1 => siblings.Single(),
			_ => throw new InvalidOperationException("Conflicting partial-suite declarations on " + TypeIdentity(suite)),
		};
	}

	private static string TypeIdentity(INamedTypeSymbol type) => type.ContainingAssembly.Identity.Name + ":" + type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

	private static string MethodIdentity(IMethodSymbol method) => TypeIdentity(method.OriginalDefinition.ContainingType) + ":" +
		method.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
}
