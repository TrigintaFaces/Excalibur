// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


//

using Microsoft.CodeAnalysis;

// Not available in netstandard2.0

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Enhanced source generator that creates a compile-time message type registry with full AOT support.
/// Generates MessageTypeRegistry, JsonTypeInfoRegistry, and module initializer for automatic registration.
/// </summary>
[Generator]
public sealed class MessageTypeRegistrySourceGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the message type registry source generator with the given context.
	/// Currently disabled to avoid conflicts with existing manual implementation.
	/// </summary>
	/// <param name="context">The generator initialization context providing access to syntax providers and source output registration.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Temporarily disabled to avoid conflicts with existing manual implementation
	}
}
