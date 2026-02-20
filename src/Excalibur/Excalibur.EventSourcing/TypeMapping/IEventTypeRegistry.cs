// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.TypeMapping;

/// <summary>
/// Maps stored event type names to CLR types, supporting renames and aliases.
/// </summary>
/// <remarks>
/// <para>
/// When an event type is renamed (e.g., <c>OrderCreated</c> becomes <c>OrderPlaced</c>),
/// the registry allows old event type names stored in the event store to resolve to the
/// current CLR type. Multiple aliases can map to the same type.
/// </para>
/// <para>
/// This follows the pattern from <c>System.Text.Json.JsonPolymorphismOptions.DerivedTypes</c>
/// which maps type discriminators to concrete types.
/// </para>
/// </remarks>
public interface IEventTypeRegistry
{
	/// <summary>
	/// Resolves a stored event type name to its current CLR type.
	/// </summary>
	/// <param name="eventTypeName">The event type name as stored in the event store.</param>
	/// <returns>The resolved CLR type, or <see langword="null"/> if the type name is not registered.</returns>
	Type? ResolveType(string eventTypeName);

	/// <summary>
	/// Gets the canonical event type name for a CLR type.
	/// </summary>
	/// <param name="eventType">The CLR type to look up.</param>
	/// <returns>The canonical event type name.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the type is not registered.</exception>
	string GetTypeName(Type eventType);

	/// <summary>
	/// Registers a mapping from an event type name to a CLR type.
	/// </summary>
	/// <param name="eventTypeName">The event type name (including aliases for renamed types).</param>
	/// <param name="eventType">The CLR type that the name maps to.</param>
	void Register(string eventTypeName, Type eventType);
}
