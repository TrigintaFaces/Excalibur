// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Thread-safe registry for managing pluggable serializers.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses <see cref="ConcurrentDictionary{TKey,TValue}" /> for thread-safe registration and lookup, with a volatile
/// reference wrapper for the current serializer to ensure visibility across threads.
/// </para>
/// <para>
/// <b> Thread-Safety Pattern: </b> The current serializer is stored in an immutable reference type using volatile semantics, ensuring that
/// updates are immediately visible to all threads without requiring locks on the hot path.
/// </para>
/// <para> See the pluggable serialization architecture documentation. </para>
/// </remarks>
public sealed class SerializerRegistry : ISerializerRegistry
{
	private readonly ConcurrentDictionary<byte, IPluggableSerializer> _serializersById = new();
	private readonly ConcurrentDictionary<string, byte> _idsByName = new(StringComparer.Ordinal);

	/// <summary>
	/// Volatile reference for thread-safe current serializer access. Using an immutable record class ensures atomic reference reads without locks.
	/// </summary>
	private volatile CurrentSerializer? _current;

	/// <summary>
	/// Immutable holder for the current serializer ID and instance. Reference types can be volatile, whereas value tuples cannot.
	/// </summary>
	private sealed record CurrentSerializer(byte Id, IPluggableSerializer Serializer);

	/// <inheritdoc />
	/// <remarks>
	/// <para>
	/// <b> Startup-Only Registration: </b> This method should only be called during application startup and DI container configuration, not
	/// at runtime. The framework registers serializers during <see cref="ISerializerRegistry" /> singleton construction via <see cref="PluggableSerializationOptions.RegistrationActions" />.
	/// </para>
	/// <para>
	/// While the implementation uses thread-safe collections, there is a brief window during name collision rollback where a serializer
	/// could theoretically be accessed by another thread. This is acceptable because:
	/// </para>
	/// <list type="number">
	/// <item> Registration occurs during single-threaded DI container setup </item>
	/// <item> The registry singleton isn't resolved until after registration completes </item>
	/// <item> Runtime registration is not a supported scenario </item>
	/// </list>
	/// </remarks>
	public void Register(byte id, IPluggableSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		// Validate ID range
		if (id is SerializerIds.Invalid or SerializerIds.Unknown)
		{
			throw new ArgumentException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.SerializerRegistry_InvalidIdRange,
					id,
					id == 0 ? "Invalid" : "Unknown"),
				nameof(id));
		}

		// Validate name is not empty
		if (string.IsNullOrWhiteSpace(serializer.Name))
		{
			throw new ArgumentException(
				Resources.SerializerRegistry_NameRequired,
				nameof(serializer));
		}

		// Attempt to register by ID
		if (!_serializersById.TryAdd(id, serializer))
		{
			throw new ArgumentException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.SerializerRegistry_IdAlreadyRegistered,
					id),
				nameof(id));
		}

		// Attempt to register by name
		if (!_idsByName.TryAdd(serializer.Name, id))
		{
			// Rollback ID registration on name collision
			_ = _serializersById.TryRemove(id, out _);
			throw new ArgumentException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.SerializerRegistry_NameAlreadyRegistered,
					serializer.Name),
				nameof(serializer));
		}
	}

	/// <inheritdoc />
	public void SetCurrent(string serializerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serializerName);

		if (!_idsByName.TryGetValue(serializerName, out var id))
		{
			var availableSerializers = string.Join(", ", _idsByName.Keys);
			throw new ArgumentException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.SerializerRegistry_SerializerNotRegistered,
					serializerName,
					availableSerializers),
				nameof(serializerName));
		}

		// Atomic update of current serializer reference
		_current = new CurrentSerializer(id, _serializersById[id]);
	}

	/// <inheritdoc />
	public (byte Id, IPluggableSerializer Serializer) GetCurrent()
	{
		var current = _current
					  ?? throw new InvalidOperationException(
						  Resources.SerializerRegistry_NoCurrentSerializer);

		return (current.Id, current.Serializer);
	}

	/// <inheritdoc />
	public IPluggableSerializer? GetById(byte id)
		=> _serializersById.GetValueOrDefault(id);

	/// <inheritdoc />
	public IReadOnlyCollection<(byte Id, string Name, IPluggableSerializer Serializer)> GetAll()
	{
		return _serializersById
			.Select(kvp => (kvp.Key, kvp.Value.Name, kvp.Value))
			.ToList();
	}
}
