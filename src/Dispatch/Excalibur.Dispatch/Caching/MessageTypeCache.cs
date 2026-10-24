// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
internal static class MessageTypeCache
{
	private const int MaxCacheEntries = 1024;

	private static readonly Lock _initLock = new();


	private sealed record CacheSnapshot(
		FrozenDictionary<Type, MessageTypeMetadata> Types,
		IReadOnlyDictionary<string, Type> Names,
		bool Initialized)
	{
		internal static CacheSnapshot Empty { get; } = new(
			FrozenDictionary<Type, MessageTypeMetadata>.Empty,
			FrozenDictionary<string, Type>.Empty,
			false);
	}

	private static CacheSnapshot _snapshot = CacheSnapshot.Empty;
	private static readonly ConcurrentDictionary<Type, MessageTypeMetadata> _fallbackTypeCache = new();

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
			if (Volatile.Read(ref _snapshot).Initialized)
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
				ClaimName(MessageNameHelper.GetDeclaredName(type), type, nameDict);
				foreach (var alias in MessageNameHelper.GetDeclaredAliases(type))
				{
					ClaimName(alias, type, nameDict);
				}
			}

			if (nullCount > 0)
			{
				logger?.LogWarning(
					"MessageTypeCache.Initialize: Filtered out {NullCount} null type entries. " +
					"This may indicate a registration issue where null types were passed to the cache. " +
					"Review your message type registration to ensure all types are non-null.",
					nullCount);
			}

			var snapshot = new CacheSnapshot(typeDict.ToFrozenDictionary(), nameDict, true);
			_fallbackTypeCache.Clear();
			Volatile.Write(ref _snapshot, snapshot);
		}
	}

	/// <summary>
	/// Gets cached metadata for the specified message type. Returns null if the type is not in the cache.
	/// </summary>
	/// <param name="messageType"> The message type to get metadata for. </param>
	/// <returns> Cached metadata or null if not found. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MessageTypeMetadata? GetMetadata(Type messageType) =>
		Volatile.Read(ref _snapshot).Types.GetValueOrDefault(messageType);

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
		if (Volatile.Read(ref _snapshot).Types.TryGetValue(messageType, out var metadata))
		{
			return metadata;
		}

		// Fallback for types not in the startup cache (rare case): cache once to avoid repeated reflection.
		if (_fallbackTypeCache.TryGetValue(messageType, out var fallbackMetadata))
		{
			return fallbackMetadata;
		}

		fallbackMetadata = new MessageTypeMetadata(messageType);

		// Bounded cache: skip caching when full to prevent unbounded memory growth
		if (_fallbackTypeCache.Count < MaxCacheEntries)
		{
			return _fallbackTypeCache.GetOrAdd(messageType, fallbackMetadata);
		}

		return fallbackMetadata;
	}

	/// <summary>
	/// Resolves a registered message type from its declared name or alias. Returns null if the type name is not in the cache.
	/// </summary>
	/// <param name="typeName"> The declared name or alias of the type. </param>
	/// <returns> The resolved type or null if not found. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Type? ResolveType(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return null;
		}

		return Volatile.Read(ref _snapshot).Names.TryGetValue(typeName, out var type)
			? type
			: null;
	}

	/// <summary>
	/// Gets the declared message name for routing, or null for an unnamed local message.
	/// </summary>
	/// <param name="messageType"> The message type. </param>
	/// <returns> The declared name, or null. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string? GetTypeName(Type messageType) => MessageNameHelper.GetDeclaredName(messageType);

	/// <summary>
	/// Checks if a type is in the cache.
	/// </summary>
	/// <param name="messageType"> The type to check. </param>
	/// <returns> True if the type is cached, false otherwise. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsCached(Type messageType) => Volatile.Read(ref _snapshot).Types.ContainsKey(messageType);

	/// <summary>
	/// Gets all cached message types.
	/// </summary>
	/// <returns> Collection of all cached types. </returns>
	public static IReadOnlyCollection<Type> GetCachedTypes() => Volatile.Read(ref _snapshot).Types.Keys;

	/// <summary>
	/// Resets the cache to its initial empty state. Intended for testing only
	/// to prevent static state contamination between parallel test runs.
	/// </summary>
	internal static void Reset()
	{
		lock (_initLock)
		{
			_fallbackTypeCache.Clear();
			Volatile.Write(ref _snapshot, CacheSnapshot.Empty);
		}
	}
	/// <summary>Claims a declared name before publishing a complete snapshot.</summary>
	/// <exception cref="InvalidOperationException">Two types claim the same declared name.</exception>
	private static void ClaimName(string? name, Type type, Dictionary<string, Type> names)
	{
		if (name is null)
		{
			return;
		}

		if (names.TryGetValue(name, out var existing) && existing != type)
		{
			throw new InvalidOperationException($"Message identity '{name}' is declared by both '{existing}' and '{type}'.");
		}

		names[name] = type;
	}

}
