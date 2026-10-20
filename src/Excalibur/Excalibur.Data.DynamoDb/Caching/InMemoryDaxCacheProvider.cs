// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Dispatch;

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
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryDaxCacheProvider"/> class.
	/// </summary>
	/// <param name="options">The DAX cache options.</param>
	/// <param name="logger">The logger instance.</param>
	public InMemoryDaxCacheProvider(
		IOptions<DaxCacheOptions> options,
		ILogger<InMemoryDaxCacheProvider> logger)
		: this(options, logger, TimeProvider.System)
	{
	}

	internal InMemoryDaxCacheProvider(
		IOptions<DaxCacheOptions> options,
		ILogger<InMemoryDaxCacheProvider> logger,
		TimeProvider timeProvider)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		var resolver = _options.JsonTypeInfoResolver;
		_jsonOptions = resolver is JsonSerializerContext context
			? new JsonSerializerOptions(context.Options)
			: new JsonSerializerOptions();
		if (resolver is null)
		{
			if (RuntimeFeature.IsDynamicCodeSupported && JsonSerializer.IsReflectionEnabledByDefault)
			{
#pragma warning disable IL2026 // Reflection metadata is reachable only when dynamic code and JSON reflection are enabled; Native AOT uses consumer metadata or the empty resolver.
				resolver = new DefaultJsonTypeInfoResolver();
#pragma warning restore IL2026
			}
			else
			{
				resolver = JsonTypeInfoResolver.Combine();
			}
		}

		_jsonOptions.TypeInfoResolver = resolver;
		_jsonOptions.MakeReadOnly();
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
			if (entry.ExpiresAt > _timeProvider.GetUtcNow())
			{
				LogCacheHit(tableName, partitionKey);
				var item = JsonSerializer.Deserialize(entry.SerializedValue, GetTypeInfo<T>());
				return Task.FromResult(item);
			}

			// Remove only the expired instance observed above; a concurrent put publishes a new instance.
			_ = _cache.TryRemove(new KeyValuePair<string, CacheEntry>(key, entry));
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
			SerializedValue = JsonSerializer.Serialize(item, GetTypeInfo<T>()),
			ExpiresAt = _timeProvider.GetUtcNow().Add(_options.CacheItemTtl)
		};

		_cache[key] = entry;
		return Task.CompletedTask;
	}

	private JsonTypeInfo<T> GetTypeInfo<T>()
	{
		try
		{
			if (_jsonOptions.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
			{
				return typeInfo;
			}
		}
		catch (NotSupportedException exception)
		{
			throw new NotSupportedException(
				$"JSON metadata for cached type '{typeof(T)}' is unavailable. Configure DaxCacheOptions.JsonTypeInfoResolver with source-generated metadata for this type and its members.",
				exception);
		}

		throw new NotSupportedException(
			$"JSON metadata for cached type '{typeof(T)}' is unavailable. Configure DaxCacheOptions.JsonTypeInfoResolver with source-generated metadata for this type and its members.");
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

	/// <summary>
	/// Composes the cache key for one item.
	/// </summary>
	/// <remarks>
	/// <para>
	/// DynamoDB key values are arbitrary strings and may contain ':' freely, so joining them with a bare
	/// ':' is not injective: pk <c>a:b</c> with sk <c>c</c> and pk <c>a</c> with sk <c>b:c</c> are two
	/// different items in one table that rendered one key. A read for either was then answered with the
	/// other's value -- a wrong answer rather than a miss, and silent.
	/// </para>
	/// <para>
	/// The absent-sort-key form made it worse by varying the ARITY: a ':' in the partition key
	/// manufactured a sort-key boundary that was never written, so a two-term key and a three-term key
	/// could collide as well.
	/// </para>
	/// <para>
	/// <see cref="SegmentedKey"/> is the framework's existing injective composer and is already used
	/// elsewhere in this package, so this reuses it rather than introducing a second encoding for one
	/// cache. It is the identity on any term containing neither ':' nor '%', so ordinary keys are
	/// unchanged; and this cache is per-process and in-memory, so no stored key shape moves.
	/// </para>
	/// </remarks>
	private static string BuildCacheKey(string tableName, string partitionKey, string? sortKey) =>
		sortKey is null
			? SegmentedKey.Compose(tableName, partitionKey)
			: SegmentedKey.Compose(tableName, partitionKey, sortKey);

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
