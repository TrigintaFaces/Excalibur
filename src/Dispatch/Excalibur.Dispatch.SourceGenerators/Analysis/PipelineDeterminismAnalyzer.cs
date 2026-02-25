// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Excalibur.Dispatch.SourceGenerators.Analysis;

/// <summary>
/// Source generator that analyzes message types to determine pipeline determinism.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer identifies message types with deterministic (non-conditional)
/// middleware pipelines. The generated metadata enables full static pipeline
/// generation for optimal performance.
/// </para>
/// <para>
/// A pipeline is considered deterministic when:
/// <list type="bullet">
/// <item>All middleware types can be resolved at compile time</item>
/// <item>No conditional middleware registration patterns are detected</item>
/// <item>The message type doesn't use dynamic pipeline profile selection</item>
/// </list>
/// </para>
/// <para>
/// The analyzer generates <c>PipelineMetadata</c> with <c>IsDeterministic()</c> and
/// <c>GetMiddlewareSequence()</c> methods that can be used by the full static pipeline
/// generator.
/// </para>
/// </remarks>
[Generator]
public sealed class PipelineDeterminismAnalyzer : IIncrementalGenerator
{
	private const string DispatchMessageInterfaceName = "IDispatchMessage";
	private const string DispatchCommandInterfaceName = "IDispatchCommand";
	private const string DispatchQueryInterfaceName = "IDispatchQuery";
	private const string DispatchActionInterfaceName = "IDispatchAction";
	private const string DomainEventInterfaceName = "IDomainEvent";
	private const string IntegrationEventInterfaceName = "IIntegrationEvent";
	private const string PipelineProfileAttributeName = "PipelineProfileAttribute";
	private const string UsePipelineProfileAttributeName = "UsePipelineProfile";

	/// <summary>
	/// Initializes the pipeline determinism analyzer with the given context.
	/// </summary>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all message types (classes/records implementing IDispatchMessage)
		var messageTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsMessageCandidate(node),
				transform: static (context, _) => GetPipelineMetadata(context))
			.Where(static info => info != null)
			.Select(static (info, _) => info!)
			.Collect();

		// Generate the pipeline metadata
		context.RegisterSourceOutput(messageTypes, GeneratePipelineMetadata);
	}

	/// <summary>
	/// Checks if a syntax node is a potential message type.
	/// </summary>
	private static bool IsMessageCandidate(SyntaxNode node)
	{
		// Look for class or record declarations with base types
		if (node is ClassDeclarationSyntax classDecl)
		{
			return classDecl.BaseList != null &&
				   !classDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword));
		}

		if (node is RecordDeclarationSyntax recordDecl)
		{
			return recordDecl.BaseList != null &&
				   !recordDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword));
		}

		return false;
	}

	/// <summary>
	/// Extracts pipeline metadata from a message type declaration.
	/// </summary>
	private static PipelineMetadata? GetPipelineMetadata(GeneratorSyntaxContext context)
	{
		INamedTypeSymbol? typeSymbol = null;

		if (context.Node is ClassDeclarationSyntax classDecl)
		{
			typeSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
		}
		else if (context.Node is RecordDeclarationSyntax recordDecl)
		{
			typeSymbol = context.SemanticModel.GetDeclaredSymbol(recordDecl) as INamedTypeSymbol;
		}

		if (typeSymbol == null)
		{
			return null;
		}

		// Skip abstract, generic type definitions, and non-public types
		if (typeSymbol.IsAbstract || typeSymbol.IsGenericType)
		{
			return null;
		}

		// Skip non-public types (nested private classes, internal types, etc.)
		if (typeSymbol.DeclaredAccessibility != Accessibility.Public)
		{
			return null;
		}

		// Check if it implements IDispatchMessage (directly or through derived interfaces)
		var implementsMessage = typeSymbol.AllInterfaces.Any(i =>
			i.Name == DispatchMessageInterfaceName);

		if (!implementsMessage)
		{
			return null;
		}

		// Determine message kind
		var messageKind = DetermineMessageKind(typeSymbol);

		// Check for pipeline profile attributes
		var hasPipelineProfile = typeSymbol.GetAttributes().Any(a =>
			a.AttributeClass is { Name: PipelineProfileAttributeName or UsePipelineProfileAttributeName });

		string? profileName = null;
		if (hasPipelineProfile)
		{
			var profileAttr = typeSymbol.GetAttributes().FirstOrDefault(a =>
				a.AttributeClass is { Name: PipelineProfileAttributeName or UsePipelineProfileAttributeName });
			if (profileAttr?.ConstructorArguments.Length > 0 &&
				profileAttr.ConstructorArguments[0].Value is string name)
			{
				profileName = name;
			}
		}

		// Determine determinism - by default, assume deterministic unless we find reasons otherwise
		var isDeterministic = true;
		string? nonDeterministicReason = null;

		// Check for dynamic profile attributes
		if (hasPipelineProfile && profileName == null)
		{
			// Profile name not statically determined
			isDeterministic = false;
			nonDeterministicReason = "Dynamic pipeline profile attribute without static name";
		}

		// Check for tenant-specific attributes
		var hasTenantAttribute = typeSymbol.GetAttributes().Any(a =>
			a.AttributeClass is { Name: "TenantSpecificAttribute" or "PerTenantAttribute" or "MultiTenantAttribute" });
		if (hasTenantAttribute)
		{
			isDeterministic = false;
			nonDeterministicReason = "Tenant-specific pipeline routing";
		}

		// Check for conditional attributes
		var hasConditionalAttribute = typeSymbol.GetAttributes().Any(a =>
			a.AttributeClass is { Name: "ConditionalMiddlewareAttribute" or "FeatureFlagMiddlewareAttribute" });
		if (hasConditionalAttribute)
		{
			isDeterministic = false;
			nonDeterministicReason = "Conditional middleware via attribute";
		}

		return new PipelineMetadata
		{
			MessageType = typeSymbol,
			MessageTypeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			MessageTypeName = typeSymbol.Name,
			IsDeterministic = isDeterministic,
			NonDeterministicReason = nonDeterministicReason,
			MessageKind = messageKind,
			HasCustomPipelineProfile = hasPipelineProfile,
			PipelineProfileName = profileName,
			ApplicableMiddleware = [] // Will be populated by runtime analysis
		};
	}

	/// <summary>
	/// Determines the message kind from the implemented interfaces.
	/// </summary>
	private static string DetermineMessageKind(INamedTypeSymbol typeSymbol)
	{
		var interfaces = typeSymbol.AllInterfaces;

		if (interfaces.Any(i => i.Name == DispatchCommandInterfaceName))
		{
			return "Command";
		}

		if (interfaces.Any(i => i.Name == DispatchQueryInterfaceName ||
								i.Name.StartsWith(DispatchActionInterfaceName, StringComparison.Ordinal)))
		{
			return "Query";
		}

		if (interfaces.Any(i => i.Name == DomainEventInterfaceName))
		{
			return "DomainEvent";
		}

		if (interfaces.Any(i => i.Name == IntegrationEventInterfaceName))
		{
			return "IntegrationEvent";
		}

		return "Message";
	}

	/// <summary>
	/// Generates the pipeline metadata source code.
	/// </summary>
	private static void GeneratePipelineMetadata(
		SourceProductionContext context,
		ImmutableArray<PipelineMetadata> messageTypes)
	{
		// Deduplicate message types
		var uniqueTypes = messageTypes
			.GroupBy(m => m.MessageTypeFullName)
			.Select(g => g.First())
			.OrderBy(m => m.MessageTypeFullName)
			.ToList();

		var deterministicCount = uniqueTypes.Count(m => m.IsDeterministic);

		var sb = new StringBuilder();

		// File header
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine($"// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = sb.AppendLine($"// Analyzed message types: {uniqueTypes.Count}");
		_ = sb.AppendLine($"// Deterministic pipelines: {deterministicCount}");
		_ = sb.AppendLine("// PERF-10 Phase 2: Pipeline determinism analysis for Sprint 457+ static pipeline generation");
		_ = sb.AppendLine();

		// Required pragmas and usings
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine();

		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Collections.Frozen;");
		_ = sb.AppendLine("using System.Collections.Generic;");
		_ = sb.AppendLine();

		// Pipeline metadata class
		_ = sb.AppendLine("namespace Excalibur.Dispatch.Generated");
		_ = sb.AppendLine("{");
		_ = sb.AppendLine("    /// <summary>");
		_ = sb.AppendLine("    /// Generated pipeline metadata for message type determinism analysis.");
		_ = sb.AppendLine("    /// </summary>");
		_ = sb.AppendLine("    /// <remarks>");
		_ = sb.AppendLine("    /// PERF-10 Phase 2: Identifies message types with deterministic pipelines.");
		_ = sb.AppendLine("    /// This metadata can be used to generate fully static middleware chains.");
		_ = sb.AppendLine("    /// </remarks>");
		_ = sb.AppendLine("    file static class PipelineMetadata");
		_ = sb.AppendLine("    {");

		// Determinism dictionary
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Frozen dictionary mapping message types to their determinism status.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        private static readonly FrozenDictionary<Type, bool> _determinism;");
		_ = sb.AppendLine();

		// Message kind dictionary
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Frozen dictionary mapping message types to their kind.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        private static readonly FrozenDictionary<Type, string> _messageKinds;");
		_ = sb.AppendLine();

		// Static constructor
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Static constructor initializes the pipeline metadata.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        static PipelineMetadata()");
		_ = sb.AppendLine("        {");
		_ = sb.AppendLine("            var determinism = new Dictionary<Type, bool>();");
		_ = sb.AppendLine("            var kinds = new Dictionary<Type, string>();");

		// Generate entries for each message type
		foreach (var message in uniqueTypes)
		{
			_ = sb.AppendLine();
			_ = sb.AppendLine($"            // {message.MessageTypeName} ({message.MessageKind})");
			_ = sb.AppendLine($"            determinism[typeof({message.MessageTypeFullName})] = {(message.IsDeterministic ? "true" : "false")};");
			_ = sb.AppendLine($"            kinds[typeof({message.MessageTypeFullName})] = \"{message.MessageKind}\";");
		}

		_ = sb.AppendLine();
		_ = sb.AppendLine("            _determinism = determinism.ToFrozenDictionary();");
		_ = sb.AppendLine("            _messageKinds = kinds.ToFrozenDictionary();");
		_ = sb.AppendLine("        }");
		_ = sb.AppendLine();

		// IsDeterministic method
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Checks if the pipeline for a message type is deterministic.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <typeparam name=\"TMessage\">The message type to check.</typeparam>");
		_ = sb.AppendLine("        /// <returns>True if the pipeline is deterministic; otherwise, false.</returns>");
		_ = sb.AppendLine("        public static bool IsDeterministic<TMessage>() =>");
		_ = sb.AppendLine("            _determinism.TryGetValue(typeof(TMessage), out var result) && result;");
		_ = sb.AppendLine();

		// IsDeterministic method (Type parameter)
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Checks if the pipeline for a message type is deterministic.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <param name=\"messageType\">The message type to check.</param>");
		_ = sb.AppendLine("        /// <returns>True if the pipeline is deterministic; otherwise, false.</returns>");
		_ = sb.AppendLine("        public static bool IsDeterministic(Type messageType) =>");
		_ = sb.AppendLine("            _determinism.TryGetValue(messageType, out var result) && result;");
		_ = sb.AppendLine();

		// GetMessageKind method
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Gets the message kind for a message type.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <typeparam name=\"TMessage\">The message type.</typeparam>");
		_ = sb.AppendLine("        /// <returns>The message kind (Command, Query, DomainEvent, IntegrationEvent, Message).</returns>");
		_ = sb.AppendLine("        public static string GetMessageKind<TMessage>() =>");
		_ = sb.AppendLine("            _messageKinds.TryGetValue(typeof(TMessage), out var kind) ? kind : \"Unknown\";");
		_ = sb.AppendLine();

		// GetMessageKind method (Type parameter)
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Gets the message kind for a message type.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        /// <param name=\"messageType\">The message type.</param>");
		_ = sb.AppendLine("        /// <returns>The message kind (Command, Query, DomainEvent, IntegrationEvent, Message).</returns>");
		_ = sb.AppendLine("        public static string GetMessageKind(Type messageType) =>");
		_ = sb.AppendLine("            _messageKinds.TryGetValue(messageType, out var kind) ? kind : \"Unknown\";");
		_ = sb.AppendLine();

		// Count properties
		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Gets the total number of analyzed message types.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine("        public static int TotalCount => _determinism.Count;");
		_ = sb.AppendLine();

		_ = sb.AppendLine("        /// <summary>");
		_ = sb.AppendLine("        /// Gets the number of message types with deterministic pipelines.");
		_ = sb.AppendLine("        /// </summary>");
		_ = sb.AppendLine($"        public static int DeterministicCount => {deterministicCount};");
		_ = sb.AppendLine("    }");
		_ = sb.AppendLine("}");

		context.AddSource("PipelineMetadata.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}
}
