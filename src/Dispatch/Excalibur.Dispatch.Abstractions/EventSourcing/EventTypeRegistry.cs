// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace Excalibur.Dispatch;

/// <summary>
/// Mutable, consumer-registrable <see cref="IEventTypeRegistry"/> that backs secure-by-default
/// event-type resolution.
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
/// Registration keys use <see cref="MessageNameHelper.GetName(System.Type)"/>, matching
/// the name the serializers persist, so a round-trip (store → resolve) is symmetric.
/// </para>
/// </remarks>
internal sealed class EventTypeRegistry : IEventTypeRegistry
{
	private readonly ConcurrentDictionary<string, Type> _byName = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<Type, string> _byType = new();

	/// <summary>
	/// Gets a value indicating whether no event types have been registered. Used by the event-sourcing
	/// startup guard to fail fast when the default type-rejecting serializer is paired with an empty
	/// allow-list (a configuration that bricks every aggregate replay).
	/// </summary>
	internal bool IsEmpty => _byName.IsEmpty;

	/// <summary>
	/// Gets the event types registered so far. The startup guard uses these to locate the assemblies a
	/// consumer keeps its events in, so it can find the siblings they did not register.
	/// </summary>
	internal IReadOnlyCollection<Type> RegisteredTypes => (IReadOnlyCollection<Type>)_byType.Keys;

	/// <summary>
	/// Registers an event type for secure name-based resolution.
	/// </summary>
	/// <param name="eventType">The event type to register.</param>
	/// <exception cref="ArgumentNullException"><paramref name="eventType"/> is <see langword="null"/>.</exception>
	public void Register(Type eventType)
	{
		ArgumentNullException.ThrowIfNull(eventType);

		// Declared or nothing. A name derived from the type would embed its namespace, assembly and
		// version, so the identity of stored data would change when any of those did.
		Register(MessageNameHelper.GetName(eventType), eventType);
	}

	/// <summary>
	/// Registers <paramref name="eventType"/> under an explicitly supplied name, for a type whose source
	/// you do not control and therefore cannot annotate.
	/// </summary>
	/// <param name="storedTypeName">The name this type is stored under.</param>
	/// <param name="eventType">The type.</param>
	/// <remarks>
	/// Identity is still declared rather than derived -- the declaration simply moves from the type to
	/// the composition root, because a type from someone else's package has nowhere to carry an
	/// attribute. Every other rule is unchanged: the name's shape is validated, and a name already held
	/// by a different type is refused.
	/// </remarks>
	internal void Register(string storedTypeName, Type eventType)
	{
		ArgumentNullException.ThrowIfNull(eventType);

		var name = MessageNameValidator.Validate(storedTypeName);
		var aliases = eventType.GetCustomAttributes<MessageNameAliasAttribute>(inherit: false)
			.Select(static a => a.Name)
			.ToArray();

		// All of a type's bindings, or none of them. Establishing the canonical name and then failing on
		// its third alias would leave the type half-registered with no way back -- and a consumer loading
		// plugins in a try/catch would keep that partial state and never know.
		foreach (var candidate in aliases.Prepend(name))
		{
			if (_byName.TryGetValue(candidate, out var holder) && holder != eventType)
			{
				throw new InvalidOperationException(
					$"The message name '{candidate}' is claimed by both '{holder}' and '{eventType}'. "
					+ "A name identifies exactly one type: stored data records the name and nothing else, "
					+ "so two types sharing one cannot be told apart when read back. Give one of them a "
					+ "different name.");
			}
		}

		Index(name, eventType);
		_byType[eventType] = name;

		foreach (var alias in aliases)
		{
			Index(alias, eventType);
		}
	}

	/// <summary>
	/// Maps <paramref name="name"/> to <paramref name="eventType"/>, refusing to let one name mean two
	/// types.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// <paramref name="name"/> is already claimed by a different type.
	/// </exception>
	/// <remarks>
	/// Names are a single global namespace, and a stored name carries no other clue to what wrote it.
	/// If two types could share one, the second registration would silently take ownership and every
	/// event stored by the first would deserialize into the wrong type -- readable, plausible, and
	/// wrong. Refused at registration, where it is a configuration error rather than corrupt data.
	/// </remarks>
	private void Index(string name, Type eventType)
	{
		if (!_byName.TryAdd(name, eventType) && _byName[name] != eventType)
		{
			throw new InvalidOperationException(
				$"The message name '{name}' is claimed by both '{_byName[name]}' and '{eventType}'. "
				+ "A name identifies exactly one type: stored data records the name and nothing else, "
				+ "so two types sharing one cannot be told apart when read back. Give one of them a "
				+ "different [MessageName].");
		}
	}

	/// <summary>
	/// Registers a historical stored type name that should resolve to <paramref name="eventType"/>.
	/// </summary>
	/// <param name="storedTypeName">The name exactly as it appears in the event store.</param>
	/// <param name="eventType">The type that name should now resolve to.</param>
	/// <remarks>
	/// This affects RESOLUTION ONLY. The reverse map is left untouched deliberately, so events written
	/// from now on continue to carry the type's current name and the alias does not propagate into new
	/// data. Use it when a type has moved namespace or assembly and the events already written still
	/// carry the name it had then.
	/// </remarks>
	/// <exception cref="ArgumentException"><paramref name="storedTypeName"/> is null or whitespace.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="eventType"/> is <see langword="null"/>.</exception>
	public void RegisterAlias(string storedTypeName, Type eventType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(storedTypeName);
		ArgumentNullException.ThrowIfNull(eventType);

		Index(storedTypeName, eventType);
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
