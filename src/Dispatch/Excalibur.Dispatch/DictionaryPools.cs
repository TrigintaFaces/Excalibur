// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.ObjectPool;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides pooled dictionary instances for high-performance scenarios.
/// </summary>
public static class DictionaryPools
{
	private static readonly ObjectPool<Dictionary<string, string>> _stringDictionaryPool;
	private static readonly ObjectPool<Dictionary<string, object?>> _objectDictionaryPool;

	static DictionaryPools()
	{
		var provider = new DefaultObjectPoolProvider();

		_stringDictionaryPool = provider.Create(new StringDictionaryPooledObjectPolicy());
		_objectDictionaryPool = provider.Create(new ObjectDictionaryPooledObjectPolicy());
	}

	/// <summary>
	/// Represents a pooled dictionary.
	/// </summary>
	internal interface IPooledMap<TKey, TValue>
		where TKey : notnull
	{
		/// <summary>
		/// Creates a pooled dictionary instance.
		/// </summary>
		PooledMap<TKey, TValue> CreatePooled();
	}

	/// <summary>
	/// Gets a pool for string-to-string dictionaries.
	/// </summary>
	/// <value>
	/// A pool for string-to-string dictionaries.
	/// </value>
	internal static IPooledMap<string, string> StringDictionary => new PooledMapWrapper<string, string>(_stringDictionaryPool);

	/// <summary>
	/// Gets a pool for string-to-object dictionaries.
	/// </summary>
	/// <value>The current <see cref="ObjectDictionary"/> value.</value>
	internal static IPooledMap<string, object?> ObjectDictionary =>
		new PooledMapWrapper<string, object?>(_objectDictionaryPool);

	/// <summary>
	/// Wraps a pooled dictionary for automatic disposal.
	/// </summary>
	internal struct PooledMap<TKey, TValue> : IDisposable, IEquatable<PooledMap<TKey, TValue>>
		where TKey : notnull
	{
		private readonly ObjectPool<Dictionary<TKey, TValue>> _pool;
		private Dictionary<TKey, TValue>? _dictionary;

		internal PooledMap(ObjectPool<Dictionary<TKey, TValue>> pool)
		{
			_pool = pool;
			_dictionary = pool.Get();
		}

		/// <summary>
		/// Gets the pooled dictionary.
		/// </summary>
		/// <value>The current <see cref="Dictionary"/> value.</value>
		/// <exception cref="ObjectDisposedException"></exception>
		public readonly Dictionary<TKey, TValue> Dictionary =>
			_dictionary ?? throw new ObjectDisposedException(nameof(PooledMap<,>));

		/// <summary>
		/// Determines whether two <see cref="PooledMap{TKey, TValue}" /> instances are equal.
		/// </summary>
		public static bool operator ==(PooledMap<TKey, TValue> left, PooledMap<TKey, TValue> right) => left.Equals(right);

		/// <summary>
		/// Determines whether two <see cref="PooledMap{TKey, TValue}" /> instances are not equal.
		/// </summary>
		public static bool operator !=(PooledMap<TKey, TValue> left, PooledMap<TKey, TValue> right) => !left.Equals(right);

		/// <summary>
		/// Returns the dictionary to the pool.
		/// </summary>
		public void Dispose()
		{
			if (_dictionary != null)
			{
				var dict = _dictionary;
				_dictionary = null;
				dict.Clear();
				_pool.Return(dict);
			}
		}

		/// <inheritdoc />
		public override readonly bool Equals(object? obj) => obj is PooledMap<TKey, TValue> other && Equals(other);

		/// <inheritdoc />
		public readonly bool Equals(PooledMap<TKey, TValue> other) =>
			ReferenceEquals(_pool, other._pool) && ReferenceEquals(_dictionary, other._dictionary);

		/// <inheritdoc />
		public override readonly int GetHashCode() => HashCode.Combine(_pool, _dictionary);
	}

	private sealed class PooledMapWrapper<TKey, TValue>(ObjectPool<Dictionary<TKey, TValue>> pool) : IPooledMap<TKey, TValue>
		where TKey : notnull
	{
		public PooledMap<TKey, TValue> CreatePooled() => new(pool);
	}

	private sealed class StringDictionaryPooledObjectPolicy : PooledObjectPolicy<Dictionary<string, string>>
	{
		public override Dictionary<string, string> Create() => new(StringComparer.Ordinal);

		public override bool Return(Dictionary<string, string> obj)
		{
			if (obj.Count > 256)
			{
				// Don't pool dictionaries that have grown too large
				return false;
			}

			obj.Clear();
			return true;
		}
	}

	private sealed class ObjectDictionaryPooledObjectPolicy : PooledObjectPolicy<Dictionary<string, object?>>
	{
		public override Dictionary<string, object?> Create() => new(StringComparer.Ordinal);

		public override bool Return(Dictionary<string, object?> obj)
		{
			if (obj.Count > 256)
			{
				// Don't pool dictionaries that have grown too large
				return false;
			}

			obj.Clear();
			return true;
		}
	}
}
