// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// AOT-safe type registry for resolving event types by name without reflection.
/// </summary>
/// <remarks>
/// <para>
/// Implementations provide compile-time type mappings (e.g., from source generators)
/// instead of runtime <c>Type.GetType()</c> / <c>Assembly.GetType()</c> calls.
/// </para>
/// <para>
/// The default implementation wraps the source-generated <c>EventStoreTypeMap</c>
/// class produced by <c>EventStoreTypeMapGenerator</c>.
/// </para>
/// </remarks>
internal interface IEventTypeRegistry
{
	/// <summary>
	/// Resolves an event type from its stored type name.
	/// </summary>
	/// <param name="eventTypeName">The event type name as stored in the event store.</param>
	/// <returns>The resolved <see cref="Type"/>, or <see langword="null"/> if the type is not registered.</returns>
	Type? ResolveType(string eventTypeName);

	/// <summary>
	/// Gets the stored type name for an event type.
	/// </summary>
	/// <param name="eventType">The event type.</param>
	/// <returns>The type name for storage, or <see langword="null"/> if the type is not registered.</returns>
	string? GetTypeName(Type eventType);
}
