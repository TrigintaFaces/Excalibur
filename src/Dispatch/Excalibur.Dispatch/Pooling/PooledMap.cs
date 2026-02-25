// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// Pooled map wrapper that returns to pool on dispose.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Value-Type Disposal Warning:</strong> This is a <c>readonly struct</c> implementing
/// <see cref="IDisposable"/>. Value-type semantics apply:
/// </para>
/// <list type="bullet">
/// <item><description>Copying this struct creates a shallow copy sharing the same underlying dictionary reference.</description></item>
/// <item><description>Disposing any copy returns the dictionary to the pool, invalidating all copies.</description></item>
/// <item><description>After disposal, accessing <see cref="Dictionary"/> on any copy may reference a reused or cleared dictionary.</description></item>
/// </list>
/// <para>
/// <strong>Best Practice:</strong> Use with <c>using</c> statement and avoid copying:
/// <code>
/// using var map = pool.RentMap&lt;string, int&gt;();
/// map.Dictionary["key"] = 42;
/// // Use dictionary
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public readonly struct PooledMap<TKey, TValue> : IDisposable, IEquatable<PooledMap<TKey, TValue>>
	where TKey : notnull
{
	private readonly DictionaryPool<TKey, TValue> _pool;

	internal PooledMap(DictionaryPool<TKey, TValue> pool)
	{
		_pool = pool;
		Dictionary = pool.Rent();
	}

	/// <summary>
	/// Gets the underlying dictionary instance managed by this pooled map.
	/// </summary>
	/// <value>The current <see cref="Dictionary"/> value.</value>
	public Dictionary<TKey, TValue> Dictionary { get; }

	/// <summary>
	/// Determines whether two pooled maps are equal.
	/// </summary>
	/// <param name="left"> The first pooled map to compare. </param>
	/// <param name="right"> The second pooled map to compare. </param>
	/// <returns> true if the pooled maps are equal; otherwise, false. </returns>
	public static bool operator ==(PooledMap<TKey, TValue> left, PooledMap<TKey, TValue> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two pooled maps are not equal.
	/// </summary>
	/// <param name="left"> The first pooled map to compare. </param>
	/// <param name="right"> The second pooled map to compare. </param>
	/// <returns> true if the pooled maps are not equal; otherwise, false. </returns>
	public static bool operator !=(PooledMap<TKey, TValue> left, PooledMap<TKey, TValue> right) => !left.Equals(right);

	/// <summary>
	/// Disposes the pooled map, returning the dictionary instance to the pool for reuse.
	/// </summary>
	public void Dispose() => _pool?.Return(Dictionary);

	/// <summary>
	/// Determines whether the specified pooled map is equal to the current pooled map.
	/// </summary>
	/// <param name="other"> The pooled map to compare with the current pooled map. </param>
	/// <returns> true if the specified pooled map is equal to the current pooled map; otherwise, false. </returns>
	public bool Equals(PooledMap<TKey, TValue> other) =>
		ReferenceEquals(Dictionary, other.Dictionary) && ReferenceEquals(_pool, other._pool);

	/// <summary>
	/// Determines whether the specified object is equal to the current pooled map.
	/// </summary>
	/// <param name="obj"> The object to compare with the current pooled map. </param>
	/// <returns> true if the specified object is equal to the current pooled map; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is PooledMap<TKey, TValue> other && Equals(other);

	/// <summary>
	/// Returns the hash code for this pooled map.
	/// </summary>
	/// <returns> A hash code for the current pooled map. </returns>
	public override int GetHashCode() => HashCode.Combine(Dictionary?.GetHashCode() ?? 0, _pool?.GetHashCode() ?? 0);
}
