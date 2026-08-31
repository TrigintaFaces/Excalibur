// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Encryption;

/// <summary>
/// Provides in-memory caching for encryption key metadata with configurable TTL.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses <see cref="MemoryCache"/> for thread-safe caching with
/// automatic expiration. It supports both sliding and absolute expiration based on
/// configuration.
/// </para>
/// <para>
/// The cache stores only key metadata, never actual key material. Telemetry is
/// reported for cache hits and misses to enable monitoring.
/// </para>
/// </remarks>
public sealed class KeyCache : IKeyCache, IKeyCacheAdmin, IDisposable
{
	private readonly MemoryCache _cache;
	private readonly KeyCacheOptions _options;
	private readonly IEncryptionTelemetryDetails? _telemetryDetails;
	private readonly ConcurrentDictionary<string, byte> _trackedKeys = new(StringComparer.Ordinal);
	private readonly Dictionary<string, KeyLock> _keyLocks = new(StringComparer.Ordinal);
	private readonly Lock _keyLocksGate = new();
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyCache"/> class with default options.
	/// </summary>
	public KeyCache()
		: this(KeyCacheOptions.Default, null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyCache"/> class.
	/// </summary>
	/// <param name="options">The cache options.</param>
	/// <param name="telemetry">Optional telemetry for reporting cache hits/misses.</param>
	public KeyCache(KeyCacheOptions options, IEncryptionTelemetry? telemetry = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		_options = options;
		_telemetryDetails = telemetry?.GetService(typeof(IEncryptionTelemetryDetails)) as IEncryptionTelemetryDetails;
		_cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = options.MaxEntries, });
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyCache"/> class.
	/// </summary>
	/// <param name="options">The cache options from dependency injection.</param>
	/// <param name="telemetry">Optional telemetry for reporting cache hits/misses.</param>
	public KeyCache(IOptions<KeyCacheOptions> options, IEncryptionTelemetry? telemetry = null)
		: this(options?.Value ?? throw new ArgumentNullException(nameof(options)), telemetry)
	{
	}

	/// <inheritdoc />
	public int Count => _trackedKeys.Count;

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetOrAddAsync(
		string keyId,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory,
		CancellationToken cancellationToken)
	{
		return await GetOrAddAsync(keyId, _options.DefaultTtl, factory, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetOrAddAsync(
		string keyId,
		TimeSpan ttl,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		ArgumentNullException.ThrowIfNull(factory);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var cacheKey = CreateCacheKey(keyId);

		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
			_telemetryDetails?.RecordCacheAccess(hit: true, "KeyCache");
			return cached;
		}

		// Per-key lock so that concurrent misses for the same key make a single factory call. The lock
		// table is rented and returned around the call, so it holds one entry per key with a call in
		// flight rather than one per key ever seen -- deduplication does not decay as key cardinality
		// grows over the process lifetime.
		var keyLock = RentKeyLock(keyId);
		try
		{
			await keyLock.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				// Double-check after acquiring lock
				if (_cache.TryGetValue(cacheKey, out cached))
				{
					_telemetryDetails?.RecordCacheAccess(hit: true, "KeyCache");
					return cached;
				}

				return await ExecuteFactoryAsync(keyId, cacheKey, ttl, factory, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_ = keyLock.Semaphore.Release();
			}
		}
		finally
		{
			ReturnKeyLock(keyId, keyLock);
		}
	}

	/// <inheritdoc />
	public KeyMetadata? TryGet(string keyId)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var cacheKey = CreateCacheKey(keyId);

		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
			_telemetryDetails?.RecordCacheAccess(hit: true, "KeyCache");
			return cached;
		}

		_telemetryDetails?.RecordCacheAccess(hit: false, "KeyCache");
		return null;
	}

	/// <inheritdoc />
	public void Set(KeyMetadata keyMetadata)
	{
		Set(keyMetadata, _options.DefaultTtl);
	}

	/// <inheritdoc />
	public void Set(KeyMetadata keyMetadata, TimeSpan ttl)
	{
		ArgumentNullException.ThrowIfNull(keyMetadata);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var cacheKey = CreateCacheKey(keyMetadata.KeyId);
		SetInternal(cacheKey, keyMetadata, ttl);
	}

	/// <inheritdoc />
	public void Remove(string keyId)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var cacheKey = CreateCacheKey(keyId);
		_cache.Remove(cacheKey);
		_ = _trackedKeys.TryRemove(cacheKey, out _);
	}

	/// <inheritdoc />
	public void Invalidate(string keyId)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Remove the main key
		Remove(keyId);

		// Also remove any version-specific entries
		var prefix = $"key:{keyId}:v";
		var keysToRemove = _trackedKeys.Keys
			.Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
			.ToList();

		foreach (var key in keysToRemove)
		{
			_cache.Remove(key);
			_ = _trackedKeys.TryRemove(key, out _);
		}
	}

	/// <inheritdoc />
	public void Clear()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		foreach (var key in _trackedKeys.Keys.ToList())
		{
			_cache.Remove(key);
		}

		_trackedKeys.Clear();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_cache.Dispose();
			_trackedKeys.Clear();

			// Any key lock still present has a factory call in flight; the caller that rented it
			// disposes it on return, so only the table is cleared here.
			lock (_keyLocksGate)
			{
				_keyLocks.Clear();
			}

			_disposed = true;
		}
	}

	private static string CreateCacheKey(string keyId) => $"key:{keyId}";

	/// <summary>
	/// Takes a reference on the lock for <paramref name="keyId"/>, creating it if no caller currently
	/// holds one. Every rental must be matched by a <see cref="ReturnKeyLock"/>.
	/// </summary>
	/// <param name="keyId">The key whose lock is required.</param>
	/// <returns>The reference-counted lock for the key.</returns>
	private KeyLock RentKeyLock(string keyId)
	{
		lock (_keyLocksGate)
		{
			if (!_keyLocks.TryGetValue(keyId, out var keyLock))
			{
				keyLock = new KeyLock();
				_keyLocks[keyId] = keyLock;
			}

			keyLock.References++;
			return keyLock;
		}
	}

	/// <summary>
	/// Releases a reference taken by <see cref="RentKeyLock"/>, removing and disposing the lock once no
	/// caller holds it.
	/// </summary>
	/// <param name="keyId">The key whose lock is being returned.</param>
	/// <param name="keyLock">The lock returned by <see cref="RentKeyLock"/>.</param>
	private void ReturnKeyLock(string keyId, KeyLock keyLock)
	{
		lock (_keyLocksGate)
		{
			if (--keyLock.References > 0)
			{
				return;
			}

			// The count reaching zero means no caller holds or is waiting on the semaphore, so both the
			// removal and the disposal are safe: a later caller for the same key creates a fresh lock.
			_ = _keyLocks.Remove(keyId);
		}

		keyLock.Semaphore.Dispose();
	}

	private async Task<KeyMetadata?> ExecuteFactoryAsync(
		string keyId,
		string cacheKey,
		TimeSpan ttl,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory,
		CancellationToken cancellationToken)
	{
		_telemetryDetails?.RecordCacheAccess(hit: false, "KeyCache");

		var metadata = await factory(keyId, cancellationToken).ConfigureAwait(false);

		if (metadata is not null)
		{
			SetInternal(cacheKey, metadata, ttl);
		}

		return metadata;
	}

	private void SetInternal(string cacheKey, KeyMetadata metadata, TimeSpan ttl)
	{
		var options = new MemoryCacheEntryOptions
		{
			Size = 1, // Each entry counts as 1 toward the size limit
		};

		if (_options.UseSlidingExpiration)
		{
			options.SlidingExpiration = ttl;
		}
		else
		{
			options.AbsoluteExpirationRelativeToNow = ttl;
		}

		_ = options.RegisterPostEvictionCallback((key, value, reason, state) =>
		{
			if (key is string keyStr)
			{
				_ = _trackedKeys.TryRemove(keyStr, out _);
			}
		});

		_ = _cache.Set(cacheKey, metadata, options);
		_trackedKeys[cacheKey] = 0;
	}

	/// <summary>
	/// A per-key mutual-exclusion primitive together with the number of callers currently using it.
	/// </summary>
	private sealed class KeyLock
	{
		/// <summary>
		/// Gets the semaphore serialising factory calls for the key.
		/// </summary>
		public SemaphoreSlim Semaphore { get; } = new(1, 1);

		/// <summary>
		/// Gets or sets the number of callers holding a rental on this lock. Read and written only
		/// under the owning cache's lock-table gate.
		/// </summary>
		public int References { get; set; }
	}
}
