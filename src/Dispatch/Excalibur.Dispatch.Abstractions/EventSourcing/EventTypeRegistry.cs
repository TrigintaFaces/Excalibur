// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch;

/// <summary>
/// Mutable, consumer-registrable <see cref="IEventTypeRegistry"/> that backs secure-by-default
/// event-type resolution (c6wd6f).
/// </summary>
/// <remarks>
/// <para>
/// Consumers populate this registry via the <c>AddEventTypes&lt;T&gt;()</c> DI helper. Both
/// <see cref="JsonEventSerializer"/> (reflection) and <c>AotJsonEventSerializer</c> consult it
/// <em>independently of any assembly scan</em>, so the secure default resolves the consumer's
/// <em>registered</em> event types without re-opening the unbounded-scan gadget-chain vector: an
/// unregistered (attacker-chosen) type stays unresolvable unless the consumer explicitly opts into
/// the reflection scan. This mirrors the .NET model (<c>JsonSerializerContext</c> /
/// <c>JsonPolymorphismOptions.DerivedTypes</c>) — secure <em>and</em> functional.
/// </para>
/// <para>
/// Registration keys use <see cref="EventTypeNameHelper.GetEventTypeName(System.Type)"/>, matching
/// the name the serializers persist, so a round-trip (store → resolve) is symmetric.
/// </para>
/// </remarks>
internal sealed class EventTypeRegistry : IEventTypeRegistry
{
	private readonly ConcurrentDictionary<string, Type> _byName = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<Type, string> _byType = new();

	/// <summary>
	/// Registers an event type for secure name-based resolution.
	/// </summary>
	/// <param name="eventType">The event type to register.</param>
	/// <exception cref="ArgumentNullException"><paramref name="eventType"/> is <see langword="null"/>.</exception>
	public void Register(Type eventType)
	{
		ArgumentNullException.ThrowIfNull(eventType);

		var name = EventTypeNameHelper.GetEventTypeName(eventType);
		_byName[name] = eventType;
		_byType[eventType] = name;
	}

	/// <inheritdoc />
	public Type? ResolveType(string eventTypeName) =>
		!string.IsNullOrEmpty(eventTypeName) && _byName.TryGetValue(eventTypeName, out var type)
			? type
			: null;

	/// <inheritdoc />
	public string? GetTypeName(Type eventType) =>
		eventType is not null && _byType.TryGetValue(eventType, out var name)
			? name
			: null;
}
