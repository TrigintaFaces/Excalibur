// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Default implementation of <see cref="IMessageMapperRegistry"/> for managing message mappers.
/// </summary>
/// <remarks>
/// <para>
/// The registry maintains a thread-safe collection of mappers and provides lookup
/// functionality based on source and target transport names. Mappers with wildcards
/// are evaluated last to allow specific mappers to take precedence.
/// </para>
/// </remarks>
public sealed class MessageMapperRegistry : IMessageMapperRegistry
{
	private readonly ConcurrentDictionary<string, IMessageMapper> _mappers = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets the count of registered mappers.
	/// </summary>
	public int Count => _mappers.Count;

	/// <inheritdoc/>
	public void Register(IMessageMapper mapper)
	{
		ArgumentNullException.ThrowIfNull(mapper);

		if (!_mappers.TryAdd(mapper.Name, mapper))
		{
			throw new InvalidOperationException($"A mapper with name '{mapper.Name}' is already registered.");
		}
	}

	/// <inheritdoc/>
	public IMessageMapper? GetMapper(string sourceTransport, string targetTransport)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceTransport);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetTransport);

		// First, look for an exact match
		var exactMapper = _mappers.Values.FirstOrDefault(m =>
			string.Equals(m.SourceTransport, sourceTransport, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(m.TargetTransport, targetTransport, StringComparison.OrdinalIgnoreCase));

		if (exactMapper is not null)
		{
			return exactMapper;
		}

		// Then, look for a mapper with wildcards
		return _mappers.Values.FirstOrDefault(m => m.CanMap(sourceTransport, targetTransport));
	}

	/// <inheritdoc/>
	public IEnumerable<IMessageMapper> GetAllMappers() => _mappers.Values;

	/// <inheritdoc/>
	public bool HasMapper(string sourceTransport, string targetTransport)
		=> GetMapper(sourceTransport, targetTransport) is not null;

	/// <summary>
	/// Removes a mapper by name.
	/// </summary>
	/// <param name="name">The name of the mapper to remove.</param>
	/// <returns><see langword="true"/> if the mapper was removed; otherwise, <see langword="false"/>.</returns>
	public bool Remove(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		return _mappers.TryRemove(name, out _);
	}

	/// <summary>
	/// Clears all registered mappers.
	/// </summary>
	public void Clear() => _mappers.Clear();

	/// <summary>
	/// Registers the default set of mappers for common transport combinations.
	/// </summary>
	/// <returns>This registry for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Registers transport-specific mappers first (most specific), followed by the
	/// wildcard default mapper as a fallback. This ordering ensures that specific
	/// mappers take precedence over the generic fallback.
	/// </para>
	/// </remarks>
	public MessageMapperRegistry RegisterDefaultMappers()
	{
		// Register specific transport mappers (most specific first)
		Register(new RabbitMqToKafkaMapper());
		Register(new KafkaToRabbitMqMapper());

		// Wildcard mapper as fallback for unmatched transport combinations
		Register(new DefaultMessageMapper("Default", DefaultMessageMapper.WildcardTransport, DefaultMessageMapper.WildcardTransport));
		return this;
	}
}
