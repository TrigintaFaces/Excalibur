// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Minimal key-value store abstraction (MS ref: <c>IDistributedCache</c> — 5 methods).
/// Advanced features (batch, atomic, admin) are available via <see cref="GetService"/>.
/// </summary>
public interface IKeyValueStore : IPersistenceProvider
{
	/// <summary>
	/// Gets the key-value store type (e.g., "Redis", "Memcached", "Hazelcast").
	/// </summary>
	/// <value>
	/// The key-value store type (e.g., "Redis", "Memcached", "Hazelcast").
	/// </value>
	string KeyValueStoreType { get; }

	/// <summary>
	/// Gets a value by its key.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="key"> The key. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The value if found; otherwise, default(T). </returns>
	Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

	/// <summary>
	/// Sets a value with the specified key.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="key"> The key. </param>
	/// <param name="value"> The value to set. </param>
	/// <param name="expiration"> Optional expiration time. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the value was set successfully; otherwise, false. </returns>
	Task<bool> SetAsync<T>(
		string key,
		T value,
		TimeSpan? expiration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a value by its key.
	/// </summary>
	/// <param name="key"> The key. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the value was deleted; otherwise, false. </returns>
	Task<bool> DeleteAsync(string key, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a key exists.
	/// </summary>
	/// <param name="key"> The key to check. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the key exists; otherwise, false. </returns>
	Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);

	/// <summary>
	/// Gets an implementation-specific service. Use to access advanced features
	/// such as <see cref="IKeyValueBatchOperations"/>, <see cref="IKeyValueAtomicOperations"/>,
	/// or <see cref="IKeyValueAdminOperations"/>.
	/// </summary>
	/// <param name="serviceType">The type of the requested service.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	new object? GetService(Type serviceType) => null;
}

/// <summary>
/// Provides batch get/set/delete for key-value stores. Obtain via
/// <see cref="IKeyValueStore.GetService"/>.
/// </summary>
public interface IKeyValueBatchOperations
{
	/// <summary>
	/// Gets multiple values by their keys.
	/// </summary>
	Task<IDictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken);

	/// <summary>
	/// Sets multiple key-value pairs.
	/// </summary>
	Task<bool> SetManyAsync<T>(IDictionary<string, T> keyValuePairs, TimeSpan? expiration, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes multiple values by their keys.
	/// </summary>
	Task<long> DeleteManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken);
}

/// <summary>
/// Provides atomic increment/decrement/expire operations. Obtain via
/// <see cref="IKeyValueStore.GetService"/>.
/// </summary>
public interface IKeyValueAtomicOperations
{
	/// <summary>
	/// Increments a numeric value by the specified amount.
	/// </summary>
	Task<long> IncrementAsync(string key, long incrementBy, CancellationToken cancellationToken);

	/// <summary>
	/// Decrements a numeric value by the specified amount.
	/// </summary>
	Task<long> DecrementAsync(string key, long decrementBy, CancellationToken cancellationToken);

	/// <summary>
	/// Sets the expiration time for a key.
	/// </summary>
	Task<bool> ExpireAsync(string key, TimeSpan expiration, CancellationToken cancellationToken);
}

/// <summary>
/// Provides administrative operations (scan, flush, raw commands). Obtain via
/// <see cref="IKeyValueStore.GetService"/>.
/// </summary>
public interface IKeyValueAdminOperations
{
	/// <summary>
	/// Gets all keys matching a pattern.
	/// </summary>
	Task<IEnumerable<string>> GetKeysAsync(string pattern, CancellationToken cancellationToken);

	/// <summary>
	/// Flushes all data from the cache.
	/// </summary>
	Task<bool> FlushAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Executes a custom command on the key-value store.
	/// </summary>
	Task<TResult> ExecuteCommandAsync<TResult>(string command, object[]? args, CancellationToken cancellationToken);
}
