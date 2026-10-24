// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Discovers SQL mutation text inside actual C# request declarations and resolves tenant parameter types.</summary>
/// <remarks>SQL keyword discovery is conservative inventory evidence; behavioral tests verify emitted predicates.</remarks>
internal static partial class TenantMutationRule
{
	[GeneratedRegex(@"\b(?:DELETE\s+FROM|UPDATE\s+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
	private static partial Regex MutationSql { get; }

	/// <summary>Aggregates SQL and anchored tenant types across source declarations.</summary>
	internal static IEnumerable<TenantMutation> Find(Compilation compilation, IAssemblySymbol tenantAssembly, string root)
	{
		var scope = new BackendProvider(tenantAssembly.GetTypeByMetadataName("Excalibur.Dispatch.TenantScope")!);
		var partition = new BackendProvider(tenantAssembly.GetTypeByMetadataName("Excalibur.Dispatch.KeyedTenantPartition")!);
		foreach (var type in ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace).Where(type => !type.IsAbstract))
		{
			var declarations = ConformanceArmWiring.Ancestors(type, includeSelf: true).SelectMany(parent => parent.DeclaringSyntaxReferences).ToArray();
			var path = declarations.Select(reference => Path.GetRelativePath(root, reference.SyntaxTree.FilePath).Replace('\\', '/'))
				.FirstOrDefault(path => path.StartsWith("src/Excalibur/", StringComparison.Ordinal) && path.Contains("/Requests/", StringComparison.Ordinal));
			if (path is null)
			{
				continue;
			}

			var mutation = false;
			var scoped = false;
			foreach (var reference in declarations)
			{
				var declaration = reference.GetSyntax();
				var model = ConformanceArmWiring.ModelFor(compilation, declaration.SyntaxTree);
				var nodes = declaration.DescendantNodes(node => node == declaration || node is not BaseTypeDeclarationSyntax).ToArray();
				mutation |= SqlText(compilation, nodes, model).Any(value => MutationSql.IsMatch(value));
				scoped |= nodes.OfType<TypeSyntax>().Select(node => model.GetTypeInfo(node).Type).Any(candidate => candidate is INamedTypeSymbol named &&
					(scope.Matches(named, compilation, model.Compilation) || partition.Matches(named, compilation, model.Compilation)));
			}

			if (mutation)
			{
				yield return new TenantMutation(new BackendProvider(type), path, scoped);
			}
		}
	}

	/// <summary>Finds request construction reachable from test methods and named data members.</summary>
	internal static string[] Bindings(Compilation compilation, IReadOnlyList<BackendProvider> requests)
	{
		var bound = new HashSet<string>(StringComparer.Ordinal);
		foreach (var suite in ConformanceForkRule.Types(compilation.Assembly.GlobalNamespace).Where(type => !type.IsAbstract && !type.IsGenericType))
		{
			if (!suite.DeclaringSyntaxReferences.Any(reference => Path.GetFileName(reference.SyntaxTree.FilePath).Contains("Conformance", StringComparison.Ordinal)))
			{
				continue;
			}

			var family = ConformanceArmWiring.Ancestors(suite, includeSelf: true).ToArray();
			var tests = ConformanceArmWiring.EffectiveMethods(suite).Where(ConformanceArmWiring.IsTest).ToArray();
			var queue = new Queue<ISymbol>(tests);
			foreach (var data in DataMembers(tests))
			{
				queue.Enqueue(data);
			}

			var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
			while (queue.TryDequeue(out var member))
			{
				if (!visited.Add(member))
				{
					continue;
				}

				foreach (var reference in member.DeclaringSyntaxReferences)
				{
					var syntax = reference.GetSyntax();
					var model = ConformanceArmWiring.ModelFor(compilation, syntax.SyntaxTree);
					foreach (var node in syntax.DescendantNodes(node => node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax).OfType<ExpressionSyntax>())
					{
						var operation = model.GetOperation(node);
						if (ConstructedType(operation) is INamedTypeSymbol named)
						{
							foreach (var request in requests.Where(request => request.Matches(named, compilation, model.Compilation)))
							{
								bound.Add(request.Identity);
							}
						}

						var target = operation switch
						{
							IInvocationOperation call when !call.TargetMethod.IsVirtual && !call.TargetMethod.IsOverride => (ISymbol)call.TargetMethod,
							IPropertyReferenceOperation property => property.Property,
							IFieldReferenceOperation field => field.Field,
							_ => null,
						};
						if (target is not null && family.Any(type => SymbolEqualityComparer.Default.Equals(type, target.ContainingType)))
						{
							queue.Enqueue(target);
						}
					}
				}
			}
		}

		return bound.Order(StringComparer.Ordinal).ToArray();
	}

	/// <summary>Resolves xUnit data members with normal inheritance hiding semantics.</summary>
	internal static IEnumerable<ISymbol> DataMembers(IEnumerable<IMethodSymbol> tests)
	{
		foreach (var test in tests)
		{
			foreach (var attribute in test.GetAttributes().Where(attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), "Xunit.MemberDataAttribute", StringComparison.Ordinal)))
			{
				if (attribute.ConstructorArguments.FirstOrDefault().Value is string member)
				{
					var owner = attribute.NamedArguments.FirstOrDefault(argument => string.Equals(argument.Key, "MemberType", StringComparison.Ordinal)).Value.Value as INamedTypeSymbol ?? test.ContainingType;
					foreach (var target in ConformanceArmWiring.Ancestors(owner, includeSelf: true).Select(type => type.GetMembers(member)).FirstOrDefault(members => members.Length > 0))
					{
						yield return target;
					}
				}
			}
		}
	}

	private static ITypeSymbol? ConstructedType(IOperation? operation) => operation switch
	{
		IObjectCreationOperation creation => creation.Type,
		IInvocationOperation call when call.TargetMethod.IsStatic => call.TargetMethod.ReturnType,
		_ => null,
	};

	private static IEnumerable<string> SqlText(Compilation root, IEnumerable<SyntaxNode> nodes, SemanticModel initialModel)
	{
		var queue = new Queue<(SyntaxNode Node, SemanticModel Model)>(nodes.Select(node => (node, initialModel)));
		var visited = new HashSet<(SyntaxTree Tree, int Start, int Length, int Kind)>();
		while (queue.TryDequeue(out var item))
		{
			if (!visited.Add((item.Node.SyntaxTree, item.Node.SpanStart, item.Node.Span.Length, item.Node.RawKind)))
			{
				continue;
			}

			if (item.Node is ExpressionSyntax expression && item.Model.GetConstantValue(expression) is { HasValue: true, Value: string text })
			{
				yield return text;
			}

			if (item.Node is InterpolatedStringExpressionSyntax interpolation)
			{
				yield return string.Join(' ', interpolation.Contents.Select(content => content switch
				{
					InterpolatedStringTextSyntax literal => literal.TextToken.ValueText,
					InterpolationSyntax hole when item.Model.GetConstantValue(hole.Expression) is { HasValue: true, Value: string constant } => constant,
					_ => " ",
				}));
			}

			var symbol = item.Model.GetOperation(item.Node) switch
			{
				IInvocationOperation call when call.TargetMethod.ReturnType.SpecialType == SpecialType.System_String => (ISymbol)call.TargetMethod,
				IFieldReferenceOperation field when field.Field.Type.SpecialType == SpecialType.System_String => field.Field,
				IPropertyReferenceOperation property when property.Property.Type.SpecialType == SpecialType.System_String => property.Property,
				_ => null,
			};
			if (symbol is null)
			{
				continue;
			}

			foreach (var reference in symbol.DeclaringSyntaxReferences)
			{
				var syntax = reference.GetSyntax();
				var model = ConformanceArmWiring.ModelFor(root, syntax.SyntaxTree);
				foreach (var node in syntax.DescendantNodesAndSelf())
				{
					queue.Enqueue((node, model));
				}
			}
		}
	}
}
