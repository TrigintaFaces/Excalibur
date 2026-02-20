// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


// using System.Diagnostics.Metrics; // Not available in netstandard2.0

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Source generator that creates compile-time mappings for Event Store types.
/// Generates event type mappings, projection handler registrations, and metadata types.
/// </summary>
[Generator]
public sealed class EventStoreTypeMapGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the Event Store type map generator with the given context.
	/// Sets up syntax providers to find event types and projection handlers,
	/// then registers source output generation for compile-time event type mappings.
	/// </summary>
	/// <param name="context">The generator initialization context providing access to syntax providers and source output registration.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all event types (classes implementing IEvent)
		var eventTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsEventCandidate(node),
				transform: static (context, _) => GetEventInfo(context))
			.Where(static info => info != null)
			.Select(static (info, _) => info!);

		// Find all projection handlers
		var projectionHandlers = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => IsProjectionHandlerCandidate(node),
				transform: static (context, _) => GetProjectionHandlerInfo(context))
			.Where(static info => info != null)
			.Select(static (info, _) => info!);

		// Generate the event metadata type
		context.RegisterSourceOutput(eventTypes.Collect(), GenerateEventMetadataType);

		// Generate the event store type map
		context.RegisterSourceOutput(eventTypes.Collect(), GenerateEventStoreTypeMap);

		// Generate projection handler registrations
		var combined = eventTypes.Collect().Combine(projectionHandlers.Collect());
		context.RegisterSourceOutput(combined, GenerateProjectionHandlerRegistry);
	}

	private static bool IsEventCandidate(SyntaxNode node) =>
		node is ClassDeclarationSyntax { BaseList: not null } classDecl &&
		!classDecl.Modifiers.Any(static m => m.IsKind(SyntaxKind.AbstractKeyword));

	private static bool IsProjectionHandlerCandidate(SyntaxNode node) =>
		node is ClassDeclarationSyntax { BaseList: not null } classDecl &&
		!classDecl.Modifiers.Any(static m => m.IsKind(SyntaxKind.AbstractKeyword));

	private static EventInfo? GetEventInfo(GeneratorSyntaxContext context)
	{
		if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol typeSymbol || typeSymbol.IsAbstract)
		{
			return null;
		}

		// Skip generic types with unbound type parameters
		if (typeSymbol.IsGenericType && typeSymbol.TypeParameters.Any())
		{
			return null;
		}

		// Check if implements IEvent or IEvent<TKey>
		var implementsEvent = typeSymbol.AllInterfaces.Any(static i =>
			i.Name == "IEvent" ||
			(i.IsGenericType && i.ConstructUnboundGenericType().Name == "IEvent"));

		if (!implementsEvent)
		{
			return null;
		}

		// Check if it's an integration event
		var isIntegrationEvent = typeSymbol.AllInterfaces.Any(static i => i.Name == "IIntegrationEvent");

		return new EventInfo
		{
			Type = typeSymbol,
			FullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			SimpleName = typeSymbol.Name,
			AssemblyQualifiedName = GetAssemblyQualifiedName(typeSymbol),
			IsIntegrationEvent = isIntegrationEvent
		};
	}

	private static ProjectionHandlerInfo? GetProjectionHandlerInfo(GeneratorSyntaxContext context)
	{
		if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol typeSymbol || typeSymbol.IsAbstract)
		{
			return null;
		}

		// Skip generic types with unbound type parameters
		if (typeSymbol.IsGenericType && typeSymbol.TypeParameters.Any())
		{
			return null;
		}

		// Check if implements IProjectionHandler or inherits from a projection base class
		var implementsProjectionHandler = typeSymbol.AllInterfaces.Any(static i =>
			i.Name is "IProjectionHandler" or "IProjection");

		var inheritsProjectionBase = typeSymbol.BaseType != null &&
									 (typeSymbol.BaseType.Name.Contains("Projection") ||
										typeSymbol.BaseType.Name.Contains("ReadModel"));

		if (!implementsProjectionHandler && !inheritsProjectionBase)
		{
			return null;
		}

		// Find handled event types by looking for Handle methods
		var handledEvents = new List<string>();

		foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
		{
			if (member.Name is "Handle" or "HandleAsync" or "Apply")
			{
				if (member.Parameters.Length > 0)
				{
					var eventParam = member.Parameters[0];
					if (eventParam.Type is INamedTypeSymbol eventType)
					{
						handledEvents.Add(GetAssemblyQualifiedName(eventType));
					}
				}
			}
		}

		if (handledEvents.Count == 0)
		{
			return null;
		}

		return new ProjectionHandlerInfo
		{
			HandlerType = typeSymbol,
			FullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
			SimpleName = typeSymbol.Name,
			HandledEventTypes = handledEvents
		};
	}

	private static string GetAssemblyQualifiedName(INamedTypeSymbol type)
	{
		var assemblyName = type.ContainingAssembly.Name;
		var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
			.Replace("global::", "");

		return $"{typeName}, {assemblyName}";
	}

	private static void GenerateEventMetadataType(SourceProductionContext context, ImmutableArray<EventInfo> events)
	{
		var sb = new StringBuilder();

		// File header
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine($"// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = sb.AppendLine();
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("namespace Excalibur.Data.EventStore;");
		_ = sb.AppendLine();

		// Generate concrete type for event metadata
		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Concrete type for event metadata to replace anonymous types (AOT-compatible).");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("public record EventMetadataContent(");
		_ = sb.AppendLine(" object AggregateKey,");
		_ = sb.AppendLine(" string ETag,");
		_ = sb.AppendLine(" DateTimeOffset OccurredOn,");
		_ = sb.AppendLine(" string SerializerVersion = \"1.0.0\");");

		context.AddSource("EventMetadataContent.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private static void GenerateEventStoreTypeMap(SourceProductionContext context, ImmutableArray<EventInfo> events)
	{
		if (events.IsDefaultOrEmpty)
		{
			return;
		}

		var sb = new StringBuilder();

		// File header
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine($"// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = sb.AppendLine($"// Event types discovered: {events.Length}");
		_ = sb.AppendLine();
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using System.Collections.Generic;");
		_ = sb.AppendLine("using System.Collections.Frozen;");
		_ = sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("namespace Excalibur.Data.EventStore;");
		_ = sb.AppendLine();

		// Generate EventStoreTypeMap class
		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Compile-time generated type map for Event Store operations (AOT-compatible).");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("[UnconditionalSuppressMessage(\"AOT\", \"IL2026:RequiresUnreferencedCode\")]");
		_ = sb.AppendLine("[UnconditionalSuppressMessage(\"AOT\", \"IL3050:RequiresDynamicCode\")]");
		_ = sb.AppendLine("public static class EventStoreTypeMap");
		_ = sb.AppendLine("{");

		// Generate type mappings
		_ = sb.AppendLine(" private static readonly FrozenDictionary<string, Type> _eventTypes;");
		_ = sb.AppendLine(" private static readonly FrozenDictionary<Type, string> _typeToName;");
		_ = sb.AppendLine();

		// Static constructor
		_ = sb.AppendLine(" static EventStoreTypeMap()");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" var eventTypes = new Dictionary<string, Type>");
		_ = sb.AppendLine(" {");

		foreach (var eventInfo in events)
		{
			_ = sb.AppendLine($" [\"{eventInfo.AssemblyQualifiedName}\"] = typeof({eventInfo.FullName}),");
		}

		_ = sb.AppendLine(" };");
		_ = sb.AppendLine();
		_ = sb.AppendLine(" var typeToName = new Dictionary<Type, string>");
		_ = sb.AppendLine(" {");

		foreach (var eventInfo in events)
		{
			_ = sb.AppendLine($" [typeof({eventInfo.FullName})] = \"{eventInfo.AssemblyQualifiedName}\",");
		}

		_ = sb.AppendLine(" };");
		_ = sb.AppendLine();
		_ = sb.AppendLine(" _eventTypes = eventTypes.ToFrozenDictionary();");
		_ = sb.AppendLine(" _typeToName = typeToName.ToFrozenDictionary();");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// GetType method
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Gets the Type for an event type name without using reflection.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static Type? GetType(string eventTypeName)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return _eventTypes.TryGetValue(eventTypeName, out var type) ? type : null;");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// GetTypeName method
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Gets the assembly qualified name for an event type.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static string? GetTypeName(Type eventType)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return _typeToName.TryGetValue(eventType, out var name) ? name : null;");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// IsEventType method
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Checks if a type name represents a known event type.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static bool IsEventType(string eventTypeName)");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return _eventTypes.ContainsKey(eventTypeName);");
		_ = sb.AppendLine(" }");
		_ = sb.AppendLine();

		// GetAllEventTypes method
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Gets all registered event types.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static IReadOnlyCollection<Type> GetAllEventTypes()");
		_ = sb.AppendLine(" {");
		_ = sb.AppendLine(" return _eventTypes.Values;");
		_ = sb.AppendLine(" }");

		_ = sb.AppendLine("}");

		context.AddSource("EventStoreTypeMap.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private static void GenerateProjectionHandlerRegistry(
		SourceProductionContext context,
		(ImmutableArray<EventInfo> Events, ImmutableArray<ProjectionHandlerInfo> Handlers) data)
	{
		if (data.Handlers.IsDefaultOrEmpty)
		{
			return;
		}

		var sb = new StringBuilder();

		// File header
		_ = sb.AppendLine("// <auto-generated/>");
		_ = sb.AppendLine($"// Generated on: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = sb.AppendLine($"// Projection handlers discovered: {data.Handlers.Length}");
		_ = sb.AppendLine();
		_ = sb.AppendLine("#nullable enable");
		_ = sb.AppendLine("using System;");
		_ = sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
		_ = sb.AppendLine("using Excalibur.Dispatch.Delivery.EventStore.Projections;");
		_ = sb.AppendLine();
		_ = sb.AppendLine("namespace Excalibur.Data.EventStore;");
		_ = sb.AppendLine();

		// Generate registration extension method
		_ = sb.AppendLine("/// <summary>");
		_ = sb.AppendLine("/// Auto-generated projection handler registrations for AOT compatibility.");
		_ = sb.AppendLine("/// </summary>");
		_ = sb.AppendLine("public static class GeneratedProjectionHandlerRegistrations");
		_ = sb.AppendLine("{");
		_ = sb.AppendLine(" /// <summary>");
		_ = sb.AppendLine(" /// Registers all discovered projection handlers.");
		_ = sb.AppendLine(" /// </summary>");
		_ = sb.AppendLine(" public static void RegisterGeneratedProjectionHandlers(");
		_ = sb.AppendLine(" this StaticProjectionEventHandlerRegistry registry,");
		_ = sb.AppendLine(" IServiceProvider serviceProvider)");
		_ = sb.AppendLine(" {");

		// Generate registrations
		foreach (var handler in data.Handlers)
		{
			_ = sb.AppendLine($" // Register {handler.SimpleName}");
			_ = sb.AppendLine($" var {handler.SimpleName.ToUpperInvariant()} = serviceProvider.GetService<{handler.FullName}>();");
			_ = sb.AppendLine($" if ({handler.SimpleName.ToUpperInvariant()} != null)");
			_ = sb.AppendLine(" {");

			foreach (var eventType in handler.HandledEventTypes)
			{
				_ = sb.AppendLine($" registry.Register<object>(\"{eventType}\", {handler.SimpleName.ToUpperInvariant()});");
			}

			_ = sb.AppendLine(" }");
			_ = sb.AppendLine();
		}

		_ = sb.AppendLine(" }");
		_ = sb.AppendLine("}");

		context.AddSource("GeneratedProjectionHandlerRegistrations.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
	}

	private sealed class EventInfo
	{
		public INamedTypeSymbol Type { get; set; } = null!;
		public string FullName { get; set; } = string.Empty;
		public string SimpleName { get; set; } = string.Empty;
		public string AssemblyQualifiedName { get; set; } = string.Empty;
		public bool IsIntegrationEvent { get; set; }
	}

	private sealed class ProjectionHandlerInfo
	{
		public INamedTypeSymbol HandlerType { get; set; } = null!;
		public string FullName { get; set; } = string.Empty;
		public string SimpleName { get; set; } = string.Empty;
		public List<string> HandledEventTypes { get; set; } = [];
	}
}
