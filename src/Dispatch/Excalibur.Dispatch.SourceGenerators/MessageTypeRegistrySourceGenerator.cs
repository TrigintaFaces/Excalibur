// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


//

using Microsoft.CodeAnalysis;

// Not available in netstandard2.0

namespace Excalibur.Dispatch.SourceGenerators;

/// <summary>
/// Archived source generator that was intended to create a compile-time aggregate message type registry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Status: ARCHIVED (Sprint 759, bd-24ktsl)</b>
/// </para>
/// <para>
/// This generator is superseded by two working alternatives:
/// <list type="bullet">
///   <item><see cref="MessageTypeSourceGenerator"/> — Active source generator that creates per-assembly
///   message type registrations at compile time (AOT-compatible).</item>
///   <item><c>Excalibur.Dispatch.Delivery.Registry.MessageTypeRegistry</c> — Manual implementation that
///   aggregates types at runtime via explicit <c>Register&lt;T&gt;()</c> calls.</item>
/// </list>
/// </para>
/// <para>
/// The class is retained (with empty <see cref="Initialize"/>) to avoid breaking the Roslyn generator
/// discovery contract. Removing a <c>[Generator]</c> class from a shipped analyzer assembly could cause
/// diagnostic warnings in consumer projects that reference the analyzer package.
/// </para>
/// </remarks>
[Generator]
public sealed class MessageTypeRegistrySourceGenerator : IIncrementalGenerator
{
	/// <summary>
	/// No-op. This generator is archived and produces no output.
	/// </summary>
	/// <param name="context">The generator initialization context.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// ARCHIVED (Sprint 759, bd-24ktsl): Superseded by MessageTypeSourceGenerator (active)
		// and manual MessageTypeRegistry. See class remarks for details.
	}
}
