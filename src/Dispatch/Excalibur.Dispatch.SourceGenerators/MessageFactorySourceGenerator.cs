// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Archived source generator that was intended to create AOT-compatible message factories
/// to replace <see cref="System.Activator.CreateInstance(Type)"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Status: ARCHIVED (Sprint 759, bd-24ktsl)</b>
/// </para>
/// <para>
/// The few <c>Activator.CreateInstance</c> call sites in the codebase are in JIT-only paths
/// (Kafka transport, Avro serializer) which already carry <c>[RequiresDynamicCode]</c> annotations.
/// AOT consumers use explicit generic DI registration patterns (e.g., <c>AddStorageQueueMessage&lt;T&gt;()</c>)
/// that bypass <c>Activator.CreateInstance</c> entirely.
/// </para>
/// <para>
/// The class is retained (with empty <see cref="Initialize"/>) to avoid breaking the Roslyn generator
/// discovery contract. Removing a <c>[Generator]</c> class from a shipped analyzer assembly could cause
/// diagnostic warnings in consumer projects that reference the analyzer package.
/// </para>
/// </remarks>
[Generator]
public sealed class MessageFactorySourceGenerator : IIncrementalGenerator
{
	/// <summary>
	/// No-op. This generator is archived and produces no output.
	/// </summary>
	/// <param name="context">The generator initialization context.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// ARCHIVED (Sprint 759, bd-24ktsl): Activator.CreateInstance calls are in JIT-only paths
		// with [RequiresDynamicCode] annotations. AOT consumers use explicit generic DI patterns.
		// See class remarks for details.
	}
}
