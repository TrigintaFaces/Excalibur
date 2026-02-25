// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.TypeMapping;

/// <summary>
/// Default implementation of <see cref="IEventTypeRegistry"/> using concurrent dictionaries
/// for thread-safe type resolution and alias chain support.
/// </summary>
/// <remarks>
/// <para>
/// Supports alias chains where an old event type name maps to an intermediate name, which in turn
/// maps to the current canonical name. The registry resolves the full chain to find the target CLR type.
/// </para>
/// </remarks>
public sealed class EventTypeRegistry : IEventTypeRegistry
{
	private readonly ConcurrentDictionary<string, Type> _nameToType = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<Type, string> _typeToName = new();
	private readonly ConcurrentDictionary<string, string> _aliases = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="EventTypeRegistry"/> class.
	/// </summary>
	/// <param name="options">The registry configuration options.</param>
	public EventTypeRegistry(IOptions<EventTypeRegistryOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var opts = options.Value;

		// Register aliases first so they can be resolved during type mapping registration.
		foreach (var (oldName, canonicalName) in opts.Aliases)
		{
			_aliases.TryAdd(oldName, canonicalName);
		}

		// Register explicit type mappings.
		foreach (var (typeName, type) in opts.TypeMappings)
		{
			Register(typeName, type);
		}
	}

	/// <inheritdoc />
	public Type? ResolveType(string eventTypeName)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventTypeName);

		// Resolve alias chain: follow aliases until we find a direct type mapping or no more aliases.
		var resolvedName = ResolveAliasChain(eventTypeName);

		return _nameToType.TryGetValue(resolvedName, out var type) ? type : null;
	}

	/// <inheritdoc />
	public string GetTypeName(Type eventType)
	{
		ArgumentNullException.ThrowIfNull(eventType);

		if (_typeToName.TryGetValue(eventType, out var name))
		{
			return name;
		}

		throw new InvalidOperationException(
			$"Type '{eventType.FullName}' is not registered in the event type registry.");
	}

	/// <inheritdoc />
	public void Register(string eventTypeName, Type eventType)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventTypeName);
		ArgumentNullException.ThrowIfNull(eventType);

		_nameToType[eventTypeName] = eventType;

		// Only set the type-to-name mapping if not already set (first registration wins as canonical).
		_typeToName.TryAdd(eventType, eventTypeName);
	}

	private string ResolveAliasChain(string eventTypeName)
	{
		var current = eventTypeName;
		var maxDepth = 10; // Guard against circular alias chains.

		while (_aliases.TryGetValue(current, out var target) && maxDepth-- > 0)
		{
			current = target;
		}

		return current;
	}
}
