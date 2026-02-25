// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Zero-allocation cache key for delegate caching. Uses struct with Type references instead of string interpolation
/// to eliminate allocations during cache key generation on every lookup.
/// </summary>
/// <remarks>
/// Use tuple/struct-based cache keys instead of string interpolation.
/// Previous pattern: $"continuation_{key}_{typeof(T).Name}_{typeof(TResult).Name}" (allocates on every call).
/// New pattern: DelegateCacheKey struct (zero-allocation, Type objects are runtime-cached).
/// </remarks>
public readonly struct DelegateCacheKey : IEquatable<DelegateCacheKey>
{
	/// <summary>
	/// Pre-computed hash code for faster dictionary lookups.
	/// </summary>
	private readonly int _hashCode;

	/// <summary>
	/// Creates a new cache key with a prefix and key.
	/// </summary>
	/// <param name="prefix">The delegate type prefix (e.g., "continuation", "error", "transform").</param>
	/// <param name="key">The user-provided cache key.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DelegateCacheKey(string prefix, string key)
	{
		Prefix = prefix;
		Key = key;
		Type1 = null;
		Type2 = null;
		_hashCode = HashCode.Combine(prefix, key);
	}

	/// <summary>
	/// Creates a new cache key with a prefix, key, and one type parameter.
	/// </summary>
	/// <param name="prefix">The delegate type prefix.</param>
	/// <param name="key">The user-provided cache key.</param>
	/// <param name="type1">The first type parameter.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DelegateCacheKey(string prefix, string key, Type type1)
	{
		Prefix = prefix;
		Key = key;
		Type1 = type1;
		Type2 = null;
		_hashCode = HashCode.Combine(prefix, key, type1);
	}

	/// <summary>
	/// Creates a new cache key with a prefix, key, and two type parameters.
	/// </summary>
	/// <param name="prefix">The delegate type prefix.</param>
	/// <param name="key">The user-provided cache key.</param>
	/// <param name="type1">The first type parameter.</param>
	/// <param name="type2">The second type parameter.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DelegateCacheKey(string prefix, string key, Type type1, Type type2)
	{
		Prefix = prefix;
		Key = key;
		Type1 = type1;
		Type2 = type2;
		_hashCode = HashCode.Combine(prefix, key, type1, type2);
	}

	/// <summary>
	/// Gets the prefix identifying the type of cached delegate.
	/// </summary>
	public string Prefix { get; }

	/// <summary>
	/// Gets the user-provided key for the cache entry.
	/// </summary>
	public string Key { get; }

	/// <summary>
	/// Gets the first type parameter, if applicable.
	/// </summary>
	public Type? Type1 { get; }

	/// <summary>
	/// Gets the second type parameter, if applicable.
	/// </summary>
	public Type? Type2 { get; }

	/// <summary>
	/// Equality operator.
	/// </summary>
	public static bool operator ==(DelegateCacheKey left, DelegateCacheKey right) => left.Equals(right);

	/// <summary>
	/// Inequality operator.
	/// </summary>
	public static bool operator !=(DelegateCacheKey left, DelegateCacheKey right) => !left.Equals(right);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(DelegateCacheKey other) =>
		string.Equals(Prefix, other.Prefix, StringComparison.Ordinal) &&
		string.Equals(Key, other.Key, StringComparison.Ordinal) &&
		Type1 == other.Type1 &&
		Type2 == other.Type2;

	/// <inheritdoc />
	public override bool Equals(object? obj) =>
		obj is DelegateCacheKey other && Equals(other);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _hashCode;

	/// <inheritdoc />
	public override string ToString() =>
		Type2 is not null ? $"{Prefix}_{Key}_{Type1?.Name}_{Type2.Name}" :
		Type1 is not null ? $"{Prefix}_{Key}_{Type1.Name}" :
		$"{Prefix}_{Key}";
}
