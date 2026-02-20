// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Generates AOT-compatible FluentValidation resolver code to replace runtime reflection.
/// </summary>
[Generator]
public sealed class FluentValidationGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the incremental generator pipeline.
	/// </summary>
	/// <param name="context">The generator initialization context.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Find all message types that have validators
		var messageProvider = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (s, _) => IsMessageType(s),
				transform: static (ctx, _) => GetMessageTypeInfo(ctx))
			.Where(static m => m is not null)
			.Collect();

		// NOTE: Validation resolver generation is currently disabled due to dependency issues.
		// The generator creates code that references AotFluentValidatorResolver which is not available in the Excalibur.Dispatch.Messaging project.
		// This will be re-enabled when the dependency structure is resolved.
		// context.RegisterSourceOutput(messageProvider, static (spc, messages) => GenerateValidationResolver(spc, messages!));
	}

	/// <summary>
	/// Determines if a syntax node represents a potential message type.
	/// </summary>
	/// <param name="node">The syntax node to examine.</param>
	/// <returns>True if the node could be a message type.</returns>
	private static bool IsMessageType(SyntaxNode node) =>
		node is ClassDeclarationSyntax { BaseList: not null } or RecordDeclarationSyntax { BaseList: not null };

	/// <summary>
	/// Extracts message type information from a generator transform context.
	/// </summary>
	/// <param name="context">The generator syntax context.</param>
	/// <returns>Message type information if valid, null otherwise.</returns>
	private static MessageTypeInfo? GetMessageTypeInfo(GeneratorSyntaxContext context)
	{
		var typeDecl = context.Node;
		var model = context.SemanticModel;
		var symbol = model.GetDeclaredSymbol(typeDecl);

		if (symbol is not INamedTypeSymbol typeSymbol)
		{
			return null;
		}

		// Check if type implements IDispatchMessage
		var implementsDispatchMessage = typeSymbol.AllInterfaces
			.Any(static i => i.Name == "IDispatchMessage");

		if (!implementsDispatchMessage)
		{
			return null;
		}

		return new MessageTypeInfo(
			typeSymbol.ToDisplayString(),
			typeSymbol.Name);
	}

	/// <summary>
	/// Information about a message type discovered during compilation.
	/// </summary>
	private sealed class MessageTypeInfo(string fullName, string shortName)
	{
		/// <summary>
		/// The fully qualified name of the message type.
		/// </summary>
		public string FullName { get; } = fullName;

		/// <summary>
		/// The short name of the message type.
		/// </summary>
		public string ShortName { get; } = shortName;
	}
}
