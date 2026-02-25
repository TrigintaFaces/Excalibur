// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DynamoDb.Caching;

/// <summary>
/// In-memory implementation of <see cref="IDaxCacheProvider"/> for development and testing.
/// In production, replace with an actual DAX SDK client implementation.
/// </summary>
internal sealed partial class InMemoryDaxCacheProvider : IDaxCacheProvider
{
	private const int MaxCacheEntries = 1024;
	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
	private readonly DaxCacheOptions _options;
	private readonly ILogger<InMemoryDaxCacheProvider> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryDaxCacheProvider"/> class.
	/// </summary>
	/// <param name="options">The DAX cache options.</param>
	/// <param name="logger">The logger instance.</param>
	public InMemoryDaxCacheProvider(
		IOptions<DaxCacheOptions> options,
		ILogger<InMemoryDaxCacheProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task<T?> GetItemAsync<T>(
		string tableName,
		string partitionKey,
		string? sortKey,
		CancellationToken cancellationToken)
		where T : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

		var key = BuildCacheKey(tableName, partitionKey, sortKey);

		if (_cache.TryGetValue(key, out var entry))
		{
			if (entry.ExpiresAt > DateTimeOffset.UtcNow)
			{
				LogCacheHit(tableName, partitionKey);
				var item = JsonSerializer.Deserialize<T>(entry.SerializedValue);
				return Task.FromResult(item);
			}

			// Expired -- remove it
			_ = _cache.TryRemove(key, out _);
		}

		LogCacheMiss(tableName, partitionKey);
		return Task.FromResult<T?>(null);
	}

	/// <inheritdoc />
	public Task PutItemAsync<T>(
		string tableName,
		string partitionKey,
		string? sortKey,
		T item,
		CancellationToken cancellationToken)
		where T : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
		ArgumentNullException.ThrowIfNull(item);

		var key = BuildCacheKey(tableName, partitionKey, sortKey);

		// Bounded cache: skip caching when full
		if (_cache.Count >= MaxCacheEntries && !_cache.ContainsKey(key))
		{
			return Task.CompletedTask;
		}

		var entry = new CacheEntry
		{
			SerializedValue = JsonSerializer.Serialize(item),
			ExpiresAt = DateTimeOffset.UtcNow.Add(_options.CacheItemTtl)
		};

		_cache[key] = entry;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task InvalidateAsync(
		string tableName,
		string partitionKey,
		string? sortKey,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

		var key = BuildCacheKey(tableName, partitionKey, sortKey);
		_ = _cache.TryRemove(key, out _);
		return Task.CompletedTask;
	}

	private static string BuildCacheKey(string tableName, string partitionKey, string? sortKey)
	{
		return sortKey is null
			? $"{tableName}:{partitionKey}"
			: $"{tableName}:{partitionKey}:{sortKey}";
	}

	[LoggerMessage(3200, LogLevel.Debug, "DAX cache hit for table '{TableName}', key '{PartitionKey}'")]
	private partial void LogCacheHit(string tableName, string partitionKey);

	[LoggerMessage(3201, LogLevel.Debug, "DAX cache miss for table '{TableName}', key '{PartitionKey}'")]
	private partial void LogCacheMiss(string tableName, string partitionKey);

	private sealed class CacheEntry
	{
		public required string SerializedValue { get; init; }
		public required DateTimeOffset ExpiresAt { get; init; }
	}
}
