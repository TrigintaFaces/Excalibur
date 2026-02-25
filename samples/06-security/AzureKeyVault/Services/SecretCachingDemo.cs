// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Security;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;

namespace AzureKeyVaultSample.Services;

/// <summary>
/// Demonstrates secret caching patterns for performance optimization.
/// </summary>
/// <remarks>
/// Azure Key Vault has rate limits, so caching secrets locally is recommended
/// for frequently accessed values. This demo shows a secure caching approach.
/// </remarks>
public sealed class SecretCachingDemo
{
	private readonly ICredentialStore _credentialStore;
	private readonly ILogger<SecretCachingDemo> _logger;
	private readonly ConcurrentDictionary<string, CachedSecret> _cache = new();
	private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

	public SecretCachingDemo(
		ICredentialStore credentialStore,
		ILogger<SecretCachingDemo> logger)
	{
		_credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task RunAsync(CancellationToken cancellationToken = default)
	{
		Console.WriteLine();
		Console.WriteLine("=== Secret Caching Demo ===");
		Console.WriteLine();

		const string secretKey = "cached-api-key";

		// First retrieval - goes to Key Vault
		_logger.LogInformation("First retrieval (cache miss)...");
		var secret1 = await GetCachedSecretAsync(secretKey, cancellationToken).ConfigureAwait(false);
		Console.WriteLine($"  Cache miss - retrieved from Key Vault");

		// Second retrieval - from cache
		_logger.LogInformation("Second retrieval (cache hit)...");
		var secret2 = await GetCachedSecretAsync(secretKey, cancellationToken).ConfigureAwait(false);
		Console.WriteLine($"  Cache hit - returned cached value");

		// Show cache statistics
		Console.WriteLine();
		Console.WriteLine("Cache Configuration:");
		Console.WriteLine($"  Cache duration: {_cacheDuration.TotalMinutes} minutes");
		Console.WriteLine($"  Cached entries: {_cache.Count}");
		Console.WriteLine();
		Console.WriteLine("Best Practices:");
		Console.WriteLine("  - Cache secrets in memory for frequently accessed values");
		Console.WriteLine("  - Use short cache durations (5-15 minutes)");
		Console.WriteLine("  - Clear cache on rotation or configuration change");
		Console.WriteLine("  - Never log or serialize SecureString values");
	}

	/// <summary>
	/// Retrieves a secret with caching for performance.
	/// </summary>
	public async Task<SecureString?> GetCachedSecretAsync(
		string key,
		CancellationToken cancellationToken = default)
	{
		// Check cache first
		if (_cache.TryGetValue(key, out var cached) && !cached.IsExpired)
		{
			_logger.LogDebug("Cache hit for {Key}", key);
			return cached.Value;
		}

		// Cache miss - fetch from Key Vault
		_logger.LogDebug("Cache miss for {Key}, fetching from Key Vault", key);
		var secret = await _credentialStore.GetCredentialAsync(key, cancellationToken).ConfigureAwait(false);

		if (secret != null)
		{
			_cache[key] = new CachedSecret(secret, _cacheDuration);
		}

		return secret;
	}

	/// <summary>
	/// Invalidates a cached secret (call on rotation).
	/// </summary>
	public void InvalidateCache(string key)
	{
		_ = _cache.TryRemove(key, out _);
		_logger.LogInformation("Cache invalidated for {Key}", key);
	}

	/// <summary>
	/// Clears all cached secrets.
	/// </summary>
	public void ClearCache()
	{
		_cache.Clear();
		_logger.LogInformation("All cached secrets cleared");
	}

	private sealed class CachedSecret
	{
		public SecureString Value { get; }
		public DateTimeOffset ExpiresAt { get; }
		public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

		public CachedSecret(SecureString value, TimeSpan duration)
		{
			Value = value;
			ExpiresAt = DateTimeOffset.UtcNow.Add(duration);
		}
	}
}
