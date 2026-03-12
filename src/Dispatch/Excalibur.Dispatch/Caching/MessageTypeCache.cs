// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// High-performance cache for message type metadata that eliminates runtime reflection and string allocations in hot paths. Uses frozen
/// collections for maximum lookup performance.
/// </summary>
public static class MessageTypeCache
{
#if NET9_0_OR_GREATER

	private static readonly Lock _initLock = new();

#else

	private static readonly object _initLock = new();

#endif

	/// <summary>
	/// Pre-computed type metadata using frozen collections for O(1) lookup performance.
	/// </summary>
	private static System.Collections.Frozen.FrozenDictionary<Type, MessageTypeMetadata> _typeCache =
		System.Collections.Frozen.FrozenDictionary<Type, MessageTypeMetadata>.Empty;
	private static readonly ConcurrentDictionary<Type, MessageTypeMetadata> _fallbackTypeCache = new();

	// Keep name lookups on an ordinal Dictionary to avoid runtime instability in FrozenDictionary<string, T>
	// for some nested-type key layouts on current .NET 10 builds.
	private static Dictionary<string, Type> _nameToTypeCache = new(StringComparer.Ordinal);
	private static volatile bool _initialized;

	/// <summary>
	/// Initializes the cache with the specified message types. This should be called once during application startup with all known message types.
	/// </summary>
	/// <param name="messageTypes"> Collection of message types to cache. </param>
	/// <param name="logger"> Optional logger for reporting initialization issues such as null type entries. </param>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2072:Target type argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.",
		Justification =
			"Message types passed to Initialize are expected to be preserved through source generation or explicit preservation. The calling code is responsible for ensuring types have required metadata.")]
	public static void Initialize(IEnumerable<Type> messageTypes, ILogger? logger = null)
	{
		ArgumentNullException.ThrowIfNull(messageTypes);

		lock (_initLock)
		{
			if (_initialized)
			{
				return;
			}

			var typeDict = new Dictionary<Type, MessageTypeMetadata>();
			var nameDict = new Dictionary<string, Type>(StringComparer.Ordinal);
			var nullCount = 0;

			foreach (var type in messageTypes)
			{
				if (type is null)
				{
					nullCount++;
					continue;
				}

				var metadata = new MessageTypeMetadata(type);
				typeDict[type] = metadata;
				nameDict[metadata.FullName] = type;

				// Also cache by simple name for flexibility
				_ = nameDict.TryAdd(metadata.SimpleName, type);
			}

			if (nullCount > 0)
			{
				logger?.LogWarning(
					"MessageTypeCache.Initialize: Filtered out {NullCount} null type entries. " +
					"This may indicate a registration issue where null types were passed to the cache. " +
					"Review your message type registration to ensure all types are non-null.",
					nullCount);
			}

			_typeCache = typeDict.ToFrozenDictionary();
			_nameToTypeCache = new Dictionary<string, Type>(nameDict, StringComparer.Ordinal);
			_fallbackTypeCache.Clear();
			_initialized = true;
		}
	}

	/// <summary>
	/// Gets cached metadata for the specified message type. Returns null if the type is not in the cache.
	/// </summary>
	/// <param name="messageType"> The message type to get metadata for. </param>
	/// <returns> Cached metadata or null if not found. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MessageTypeMetadata? GetMetadata(Type messageType) =>
		_typeCache.GetValueOrDefault(messageType);

	/// <summary>
	/// Gets cached metadata for the specified message type, computing it if not found.
	/// </summary>
	/// <param name="messageType"> The message type to get metadata for. </param>
	/// <returns> Cached or computed metadata. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MessageTypeMetadata GetOrCreateMetadata(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
		Type messageType)
	{
		if (_typeCache.TryGetValue(messageType, out var metadata))
		{
			return metadata;
		}

		// Fallback for types not in the startup cache (rare case): cache once to avoid repeated reflection.
		if (_fallbackTypeCache.TryGetValue(messageType, out var fallbackMetadata))
		{
			return fallbackMetadata;
		}

		fallbackMetadata = new MessageTypeMetadata(messageType);
		return _fallbackTypeCache.GetOrAdd(messageType, fallbackMetadata);
	}

	/// <summary>
	/// Resolves a message type from its name. Returns null if the type name is not in the cache.
	/// </summary>
	/// <param name="typeName"> The full or simple name of the type. </param>
	/// <returns> The resolved type or null if not found. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Type? ResolveType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return null;
		}

		return _nameToTypeCache.TryGetValue(typeName, out var type)
			? type
			: null;
	}

	/// <summary>
	/// Gets the type name for routing purposes without allocation.
	/// </summary>
	/// <param name="messageType"> The message type. </param>
	/// <returns> The cached type name. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string GetTypeName(Type messageType)
	{
		ArgumentNullException.ThrowIfNull(messageType);
		return _typeCache.TryGetValue(messageType, out var metadata)
			? metadata.FullName
			: _fallbackTypeCache.TryGetValue(messageType, out var fallbackMetadata)
				? fallbackMetadata.FullName
				: messageType.FullName ?? messageType.Name;
	}

	/// <summary>
	/// Checks if a type is in the cache.
	/// </summary>
	/// <param name="messageType"> The type to check. </param>
	/// <returns> True if the type is cached, false otherwise. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsCached(Type messageType) => _typeCache.ContainsKey(messageType);

	/// <summary>
	/// Gets all cached message types.
	/// </summary>
	/// <returns> Collection of all cached types. </returns>
	public static IReadOnlyCollection<Type> GetCachedTypes() => _typeCache.Keys;

	/// <summary>
	/// Resets the cache to its initial empty state. Intended for testing only
	/// to prevent static state contamination between parallel test runs.
	/// </summary>
	internal static void Reset()
	{
		lock (_initLock)
		{
			_typeCache = System.Collections.Frozen.FrozenDictionary<Type, MessageTypeMetadata>.Empty;
			_nameToTypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
			_fallbackTypeCache.Clear();
			_initialized = false;
		}
	}
}
