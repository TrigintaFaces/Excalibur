// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Excalibur.Dispatch.SourceGenerators.Analysis;

/// <summary>
/// Analyzes middleware implementations to determine if they can be decomposed into
/// Before/After phases for static pipeline generation.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer examines the <c>InvokeAsync</c> method of each
/// middleware to identify:
/// <list type="bullet">
/// <item>Before-phase code: Statements before the <c>await next()</c> call</item>
/// <item>After-phase code: Statements after the <c>await next()</c> call</item>
/// <item>State variables: Variables declared before and used after next()</item>
/// <item>Control flow patterns: try/catch/finally, using statements</item>
/// </list>
/// </para>
/// <para>
/// Middleware that CANNOT be decomposed includes:
/// <list type="bullet">
/// <item>Multiple next() calls (retry patterns)</item>
/// <item>next() inside loops</item>
/// <item>Conditional next() based on runtime values</item>
/// <item>Async enumerable patterns</item>
/// </list>
/// </para>
/// </remarks>
[Generator]
public sealed class MiddlewareDecompositionAnalyzer : IIncrementalGenerator
{
	private const string DispatchMiddlewareInterfaceName = "IDispatchMiddleware";
	private const string InvokeAsyncMethodName = "InvokeAsync";
	private const string NextDelegateParameterName = "nextDelegate";

	/// <summary>
	/// Initializes the middleware decomposition analyzer with the given context.
	/// </summary>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all middleware InvokeAsync method bodies
		var middlewareTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsMiddlewareCandidate(node),
				transform: static (context, _) => AnalyzeMiddleware(context))
			.Where(static info => info != null)
			.Select(static (info, _) => info!)
			.Collect();

		// Generate the decomposition metadata
		context.RegisterSourceOutput(middlewareTypes, GenerateDecompositionMetadata);
	}

	/// <summary>
	/// Checks if a syntax node is a potential middleware class.
	/// </summary>
	private static bool IsMiddlewareCandidate(SyntaxNode node)
	{
		// Look for class declarations with base types
		if (node is not ClassDeclarationSyntax classDecl)
		{
			return false;
		}

		// Must have base list (implements interface)
		if (classDecl.BaseList == null)
		{
			return false;
		}

		// Must not be abstract
		if (classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Analyzes a middleware class to determine decomposability.
	/// </summary>
	private static MiddlewareDecomposition? AnalyzeMiddleware(GeneratorSyntaxContext context)
	{
		var classDecl = (ClassDeclarationSyntax)context.Node;
		var semanticModel = context.SemanticModel;

		if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol typeSymbol)
		{
			return null;
		}

		// Skip abstract, generic, and non-public types
		if (typeSymbol.IsAbstract || typeSymbol.IsGenericType)
		{
			return null;
		}

		// Check if it implements IDispatchMiddleware
		var implementsMiddleware = typeSymbol.AllInterfaces.Any(i =>
			i.Name == DispatchMiddlewareInterfaceName);

		if (!implementsMiddleware)
		{
			return null;
		}

		// Find the InvokeAsync method
		var invokeAsyncMethod = typeSymbol.GetMembers(InvokeAsyncMethodName)
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.Length == 4 &&
								 m.DeclaredAccessibility == Accessibility.Public);

		if (invokeAsyncMethod == null)
		{
			return null;
		}

		// Get the method syntax
		var methodSyntax = invokeAsyncMethod.DeclaringSyntaxReferences
			.Select(r => r.GetSyntax())
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault();

		if (methodSyntax?.Body == null && methodSyntax?.ExpressionBody == null)
		{
			return null;
		}

		// Analyze the method body for decomposability
		var decomposition = new MiddlewareDecomposition
		{
			MiddlewareType = typeSymbol,
			MiddlewareTypeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			MiddlewareTypeName = typeSymbol.Name,
			IsDecomposable = true // Assume decomposable until proven otherwise
		};

		// Check for Stage property
		var stageProperty = typeSymbol.GetMembers("Stage")
			.OfType<IPropertySymbol>()
			.FirstOrDefault();
		if (stageProperty != null)
		{
			// Try to extract stage value from implementation
			decomposition.Stage = ExtractStageValue(typeSymbol, semanticModel);
		}

		// Analyze the method body
		if (methodSyntax?.Body != null)
		{
			AnalyzeMethodBody(methodSyntax.Body, semanticModel, decomposition);
		}
		else if (methodSyntax?.ExpressionBody != null)
		{
			AnalyzeExpressionBody(methodSyntax.ExpressionBody, semanticModel, decomposition);
		}

		return decomposition;
	}

	/// <summary>
	/// Analyzes a method body to determine decomposition characteristics.
	/// </summary>
	private static void AnalyzeMethodBody(
		BlockSyntax body,
		SemanticModel semanticModel,
		MiddlewareDecomposition decomposition)
	{
		// Find all next() delegate invocations
		var nextCalls = body.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Where(inv => IsNextDelegateCall(inv, semanticModel))
			.ToList();

		// Rule: Multiple next() calls = not decomposable (retry patterns)
		if (nextCalls.Count > 1)
		{
			decomposition.IsDecomposable = false;
			decomposition.NonDecomposableReason = "Multiple next() delegate calls detected (likely retry pattern)";
			return;
		}

		// Rule: No next() call = not decomposable (invalid middleware)
		if (nextCalls.Count == 0)
		{
			decomposition.IsDecomposable = false;
			decomposition.NonDecomposableReason = "No next() delegate call found";
			return;
		}

		var nextCall = nextCalls[0];

		// Rule: next() inside a loop = not decomposable
		if (IsInsideLoop(nextCall))
		{
			decomposition.IsDecomposable = false;
			decomposition.NonDecomposableReason = "next() call is inside a loop (retry pattern)";
			return;
		}

		// Rule: next() inside conditional with runtime evaluation = not decomposable
		if (IsInsideRuntimeConditional(nextCall, semanticModel))
		{
			decomposition.IsDecomposable = false;
			decomposition.NonDecomposableReason = "next() call is inside conditional based on runtime values";
			return;
		}

		// Check for try/catch/finally patterns
		var tryStatement = nextCall.Ancestors().OfType<TryStatementSyntax>().FirstOrDefault();
		if (tryStatement != null)
		{
			decomposition.HasTryCatch = tryStatement.Catches.Count > 0;
			decomposition.HasFinally = tryStatement.Finally != null;
		}

		// Check for using statements
		decomposition.HasUsing = nextCall.Ancestors().OfType<UsingStatementSyntax>().Any() ||
								 body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
									 .Any(l => l.UsingKeyword.IsKind(SyntaxKind.UsingKeyword));

		// Determine before/after phases by position relative to next() call
		var statements = body.Statements;
		var nextCallStatement = nextCall.Ancestors().OfType<StatementSyntax>().First();
		var nextCallIndex = statements.IndexOf(s => s.Contains(nextCallStatement) || s == nextCallStatement);

		decomposition.HasBeforePhase = nextCallIndex > 0 ||
									   HasStatementsBeforeNextCall(body, nextCall);

		decomposition.HasAfterPhase = HasStatementsAfterNextCall(body, nextCall) ||
									  decomposition.HasFinally;

		// Check for early return patterns (short-circuit)
		decomposition.CanShortCircuit = body.DescendantNodes()
			.OfType<ReturnStatementSyntax>()
			.Any(r => IsBeforeNextCall(r, nextCall));

		// Extract state variables (simplified - variables declared before next() and used after)
		ExtractStateVariables(body, nextCall, semanticModel, decomposition);
	}

	/// <summary>
	/// Analyzes an expression body (arrow expression) for decomposition.
	/// </summary>
	private static void AnalyzeExpressionBody(
		ArrowExpressionClauseSyntax expressionBody,
		SemanticModel semanticModel,
		MiddlewareDecomposition decomposition)
	{
		// Expression bodies are typically simple pass-throughs like:
		// => next(message, context, ct);
		// These have no before/after phases
		var invocation = expressionBody.Expression as AwaitExpressionSyntax;
		if (invocation?.Expression is InvocationExpressionSyntax inv &&
			IsNextDelegateCall(inv, semanticModel))
		{
			decomposition.IsDecomposable = true;
			decomposition.HasBeforePhase = false;
			decomposition.HasAfterPhase = false;
		}
		else
		{
			// More complex expression body - mark as having both phases
			decomposition.IsDecomposable = true;
			decomposition.HasBeforePhase = true;
			decomposition.HasAfterPhase = false;
		}
	}

	/// <summary>
	/// Checks if an invocation is a call to the next() delegate.
	/// </summary>
	private static bool IsNextDelegateCall(InvocationExpressionSyntax invocation, SemanticModel _)
	{
		// Check if it's calling a parameter named "nextDelegate" or "next"
		if (invocation.Expression is IdentifierNameSyntax identifier)
		{
			var name = identifier.Identifier.Text;
			return name is NextDelegateParameterName or "next";
		}

		// Could also be member access on a field storing the delegate
		return false;
	}

	/// <summary>
	/// Checks if a node is inside a loop construct.
	/// </summary>
	private static bool IsInsideLoop(SyntaxNode node)
	{
		return node.Ancestors().Any(a =>
			a is ForStatementSyntax or
				ForEachStatementSyntax or
				WhileStatementSyntax or
				DoStatementSyntax);
	}

	/// <summary>
	/// Checks if a node is inside a conditional that depends on runtime values.
	/// </summary>
	private static bool IsInsideRuntimeConditional(SyntaxNode node, SemanticModel semanticModel)
	{
		var ifStatements = node.Ancestors().OfType<IfStatementSyntax>();
		foreach (var ifStatement in ifStatements)
		{
			// Check if the condition involves message properties or other runtime values
			var condition = ifStatement.Condition;
			var symbols = condition.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Select(i => semanticModel.GetSymbolInfo(i).Symbol)
				.Where(s => s != null);

			foreach (var symbol in symbols)
			{
				// If condition references parameters (especially message), it's runtime-dependent
				if (symbol is IParameterSymbol param &&
					param.Name is "message" or "context")
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Checks if there are statements before the next() call.
	/// </summary>
	private static bool HasStatementsBeforeNextCall(BlockSyntax body, InvocationExpressionSyntax nextCall)
	{
		var nextCallSpan = nextCall.SpanStart;
		return body.DescendantNodes()
			.OfType<StatementSyntax>()
			.Any(s => s.SpanStart < nextCallSpan &&
					  !s.Contains(nextCall) &&
					  s is not ReturnStatementSyntax);
	}

	/// <summary>
	/// Checks if there are statements after the next() call.
	/// </summary>
	private static bool HasStatementsAfterNextCall(BlockSyntax body, InvocationExpressionSyntax nextCall)
	{
		var nextCallSpan = nextCall.Span.End;
		return body.DescendantNodes()
			.OfType<StatementSyntax>()
			.Any(s => s.SpanStart > nextCallSpan &&
					  !s.Contains(nextCall));
	}

	/// <summary>
	/// Checks if a return statement is before the next() call.
	/// </summary>
	private static bool IsBeforeNextCall(ReturnStatementSyntax returnStatement, InvocationExpressionSyntax nextCall)
	{
		return returnStatement.SpanStart < nextCall.SpanStart;
	}

	/// <summary>
	/// Extracts state variables that need to be passed from Before to After phase.
	/// </summary>
	private static void ExtractStateVariables(
		BlockSyntax body,
		InvocationExpressionSyntax nextCall,
		SemanticModel semanticModel,
		MiddlewareDecomposition decomposition)
	{
		var nextCallSpan = nextCall.SpanStart;

		// Find variable declarations before next()
		var declarationsBefore = body.DescendantNodes()
			.OfType<LocalDeclarationStatementSyntax>()
			.Where(d => d.SpanStart < nextCallSpan)
			.SelectMany(d => d.Declaration.Variables);

		// Find identifiers used after next()
		var identifiersAfter = body.DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Where(i => i.SpanStart > nextCall.Span.End);

		var afterIdentifierNames = new HashSet<string>(
			identifiersAfter.Select(i => i.Identifier.Text));

		foreach (var declaration in declarationsBefore)
		{
			var name = declaration.Identifier.Text;
			if (afterIdentifierNames.Contains(name))
			{
				// This variable is used across the next() call - needs state capture
				if (semanticModel.GetDeclaredSymbol(declaration) is ILocalSymbol symbol)
				{
					var stateVar = new StateVariable
					{
						Name = name,
						TypeFullName = symbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
						IsNullable = symbol.Type.NullableAnnotation == NullableAnnotation.Annotated,
						RequiresDisposal = symbol.Type.AllInterfaces.Any(i => i.Name == "IDisposable")
					};
					decomposition.StateVariables.Add(stateVar);
				}
			}
		}
	}

	/// <summary>
	/// Tries to extract the Stage property value from a middleware type.
	/// </summary>
	private static int? ExtractStageValue(INamedTypeSymbol typeSymbol, SemanticModel _)
	{
		// Look for explicit stage implementations
		var stageProperty = typeSymbol.GetMembers("Stage")
			.OfType<IPropertySymbol>()
			.FirstOrDefault();

		if (stageProperty == null)
		{
			return null;
		}

		// Try to get the constant value if it's a simple getter
		// This is a simplified approach - full implementation would need to
		// evaluate the property initializer or getter body
		return null;
	}

	/// <summary>
	/// Generates the decomposition metadata source code.
	/// </summary>
	private static void GenerateDecompositionMetadata(
		SourceProductionContext context,
		ImmutableArray<MiddlewareDecomposition> middlewareTypes)
	{
		// Deduplicate
		var uniqueTypes = middlewareTypes
			.GroupBy(m => m.MiddlewareTypeFullName)
			.Select(g => g.First())
			.OrderBy(m => m.MiddlewareTypeFullName)
			.ToList();

		var decomposableCount = uniqueTypes.Count(m => m.IsDecomposable);

		var sb = new StringBuilder();

		// File header
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine($"// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = sb.AppendLine($"// Analyzed middleware types: {uniqueTypes.Count}");
		_ = sb.AppendLine($"// Decomposable middleware: {decomposableCount}");
		_ = sb.AppendLine("// PERF-23 (Sprint 457): Middleware decomposition analysis for static pipeline generation");
		_ = sb.AppendLine();

		// Required pragmas and usings
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine();

		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Collections.Frozen;");
		_ = sb.AppendLine("using System.Collections.Generic;");
		_ = sb.AppendLine();

		// Decomposition metadata class
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Generated");
		_ = sb.AppendLine("{");
		_ = sb.AppendLine("    /// <summary>");
		_ = sb.AppendLine("    /// Generated middleware decomposition metadata for static pipeline generation.");
		_ = sb.AppendLine("    /// </summary>");
		_ = sb.AppendLine("    /// <remarks>");
		_ = sb.AppendLine("    /// Identifies middleware that can be statically inlined");
		_ = sb.AppendLine("    /// by decomposing their InvokeAsync into Before/After phases.");
		_ = sb.AppendLine("    /// </remarks>");
		_ = sb.AppendLine("    file static class MiddlewareDecompositionMetadata");
		_ = sb.AppendLine("    {");

		// Decomposability dictionary
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Frozen dictionary mapping middleware types to their decomposability status.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        private static readonly FrozenDictionary<Type, DecompositionInfo> _decompositions;");
		_ = sb.AppendLine();

		// Static constructor
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Static constructor initializes the decomposition metadata.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        static MiddlewareDecompositionMetadata()");
		_ = sb.AppendLine("        {");
		_ = sb.AppendLine("            var dict = new Dictionary<Type, DecompositionInfo>();");

		foreach (var middleware in uniqueTypes)
		{
			_ = sb.AppendLine();
			_ = sb.AppendLine($"            // {middleware.MiddlewareTypeName}");
			_ = sb.AppendLine($"            dict[typeof({middleware.MiddlewareTypeFullName})] = new DecompositionInfo(");
			_ = sb.AppendLine($"                IsDecomposable: {(middleware.IsDecomposable ? "true" : "false")},");
			_ = sb.AppendLine($"                HasBeforePhase: {(middleware.HasBeforePhase ? "true" : "false")},");
			_ = sb.AppendLine($"                HasAfterPhase: {(middleware.HasAfterPhase ? "true" : "false")},");
			_ = sb.AppendLine($"                HasTryCatch: {(middleware.HasTryCatch ? "true" : "false")},");
			_ = sb.AppendLine($"                HasFinally: {(middleware.HasFinally ? "true" : "false")},");
			_ = sb.AppendLine($"                HasUsing: {(middleware.HasUsing ? "true" : "false")},");
			_ = sb.AppendLine($"                CanShortCircuit: {(middleware.CanShortCircuit ? "true" : "false")},");
			_ = sb.AppendLine($"                StateVariableCount: {middleware.StateVariables.Count},");
			_ = sb.AppendLine($"                NonDecomposableReason: {(middleware.NonDecomposableReason != null ? $"\"{EscapeString(middleware.NonDecomposableReason)}\"" : "null")});");
		}

		_ = sb.AppendLine();
		_ = sb.AppendLine("            _decompositions = dict.ToFrozenDictionary();");
		_ = sb.AppendLine("        }");
		_ = sb.AppendLine();

		// IsDecomposable method
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Checks if a middleware type can be decomposed for static inlining.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <typeparam name=\"TMiddleware\">The middleware type to check.</typeparam>");
		_ = sb.AppendLine("        /// <returns>True if decomposable; otherwise, false.</returns>");
		_ = sb.AppendLine("        public static bool IsDecomposable<TMiddleware>() =>");
		_ = sb.AppendLine("            _decompositions.TryGetValue(typeof(TMiddleware), out var info) && info.IsDecomposable;");
		_ = sb.AppendLine();

		// IsDecomposable method (Type parameter)
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Checks if a middleware type can be decomposed for static inlining.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <param name=\"middlewareType\">The middleware type to check.</param>");
		_ = sb.AppendLine("        /// <returns>True if decomposable; otherwise, false.</returns>");
		_ = sb.AppendLine("        public static bool IsDecomposable(Type middlewareType) =>");
		_ = sb.AppendLine("            _decompositions.TryGetValue(middlewareType, out var info) && info.IsDecomposable;");
		_ = sb.AppendLine();

		// GetInfo method
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Gets decomposition information for a middleware type.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <param name=\"middlewareType\">The middleware type.</param>");
		_ = sb.AppendLine("        /// <returns>The decomposition info, or null if not analyzed.</returns>");
		_ = sb.AppendLine("        public static DecompositionInfo? GetInfo(Type middlewareType) =>");
		_ = sb.AppendLine("            _decompositions.TryGetValue(middlewareType, out var info) ? info : null;");
		_ = sb.AppendLine();

		// Count properties
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Gets the total number of analyzed middleware types.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        public static int TotalCount => _decompositions.Count;");
		_ = sb.AppendLine();

		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Gets the number of decomposable middleware types.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine($"        public static int DecomposableCount => {decomposableCount};");
		_ = sb.AppendLine("    }");
		_ = sb.AppendLine();

		// DecompositionInfo record
		_ = sb.AppendLine("    /// <summary>");
		_ = sb.AppendLine("    /// Information about a middleware's decomposition characteristics.");
		_ = sb.AppendLine("    /// </summary>");
		_ = sb.AppendLine("    file readonly record struct DecompositionInfo(");
		_ = sb.AppendLine("        bool IsDecomposable,");
		_ = sb.AppendLine("        bool HasBeforePhase,");
		_ = sb.AppendLine("        bool HasAfterPhase,");
		_ = sb.AppendLine("        bool HasTryCatch,");
		_ = sb.AppendLine("        bool HasFinally,");
		_ = sb.AppendLine("        bool HasUsing,");
		_ = sb.AppendLine("        bool CanShortCircuit,");
		_ = sb.AppendLine("        int StateVariableCount,");
		_ = sb.AppendLine("        string? NonDecomposableReason);");
		_ = sb.AppendLine("}");

		context.AddSource("MiddlewareDecomposition.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	/// <summary>
	/// Escapes a string for C# string literal.
	/// </summary>
	private static string EscapeString(string value) =>
		value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
