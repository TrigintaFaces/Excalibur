// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Source generator that creates AOT-compatible message factories to replace Activator.CreateInstance. Generates compile-time factories
/// for all IDispatchMessage implementations with parameterless constructors.
/// </summary>
[Generator]
public sealed class MessageFactorySourceGenerator : IIncrementalGenerator
{
	/// <summary>
	/// Initializes the message factory source generator with the given context. Currently disabled to avoid conflicts with existing
	/// manual implementations.
	/// </summary>
	/// <param name="context"> The generator initialization context providing access to syntax providers and source output registration. </param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Temporarily disabled to avoid conflicts with existing manual implementation
	}

	private struct MessageFactoryInfo
	{
		public string FullName { get; set; }
		public string Name { get; set; }
		public string Namespace { get; set; }
		public bool IsRecord { get; set; }
	}
}
