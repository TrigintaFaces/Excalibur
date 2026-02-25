// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// A caching decorator for <see cref="ISchemaRegistryClient"/> that caches schema lookups.
/// </summary>
/// <remarks>
/// <para>
/// This decorator caches:
/// </para>
/// <list type="bullet">
///   <item><description>Schema ID lookups by (subject, schema hash)</description></item>
///   <item><description>Schema retrievals by schema ID</description></item>
///   <item><description>Optionally, compatibility check results</description></item>
/// </list>
/// <para>
/// Schema strings are hashed for cache keys to avoid large memory consumption
/// for key storage.
/// </para>
/// </remarks>
public sealed partial class CachingSchemaRegistryClient : ISchemaRegistryClient
{
	private const string SchemaIdPrefix = "schema:";
	private const string SchemaByIdPrefix = "id:";
	private const string CompatibilityPrefix = "compat:";

	private readonly ISchemaRegistryClient _inner;
	private readonly IMemoryCache _cache;
	private readonly CachingSchemaRegistryOptions _options;
	private readonly ILogger<CachingSchemaRegistryClient> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CachingSchemaRegistryClient"/> class.
	/// </summary>
	/// <param name="inner">The inner schema registry client to wrap.</param>
	/// <param name="cache">The memory cache instance.</param>
	/// <param name="options">The caching options.</param>
	/// <param name="logger">The logger instance.</param>
	public CachingSchemaRegistryClient(
		ISchemaRegistryClient inner,
		IMemoryCache cache,
		CachingSchemaRegistryOptions options,
		ILogger<CachingSchemaRegistryClient> logger)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_cache = cache ?? throw new ArgumentNullException(nameof(cache));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<int> GetSchemaIdAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken)
	{
		var schemaHash = ComputeSchemaHash(schema);
		var cacheKey = $"{SchemaIdPrefix}{subject}:{schemaHash}";

		if (_cache.TryGetValue(cacheKey, out int cachedSchemaId))
		{
			LogCacheHit("GetSchemaId", subject);
			return cachedSchemaId;
		}

		LogCacheMiss("GetSchemaId", subject);
		var schemaId = await _inner.GetSchemaIdAsync(subject, schema, cancellationToken).ConfigureAwait(false);

		var cacheOptions = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = _options.CacheDuration,
			Size = 1
		};

		_ = _cache.Set(cacheKey, schemaId, cacheOptions);

		return schemaId;
	}

	/// <inheritdoc/>
	public async Task<string> GetSchemaByIdAsync(
		int schemaId,
		CancellationToken cancellationToken)
	{
		var cacheKey = $"{SchemaByIdPrefix}{schemaId}";

		if (_cache.TryGetValue(cacheKey, out string? cachedSchema) && cachedSchema is not null)
		{
			LogCacheHitById("GetSchemaById", schemaId);
			return cachedSchema;
		}

		LogCacheMissById("GetSchemaById", schemaId);
		var schema = await _inner.GetSchemaByIdAsync(schemaId, cancellationToken).ConfigureAwait(false);

		var cacheOptions = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = _options.CacheDuration,
			// Size is proportional to schema string length
			Size = 1 + (schema.Length / 1000)
		};

		_ = _cache.Set(cacheKey, schema, cacheOptions);

		return schema;
	}

	/// <inheritdoc/>
	public async Task<int> RegisterSchemaAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken)
	{
		// Registration is not cached - always hits the registry
		var schemaId = await _inner.RegisterSchemaAsync(subject, schema, cancellationToken).ConfigureAwait(false);

		// But we can cache the result for future lookups
		var schemaHash = ComputeSchemaHash(schema);
		var schemaIdCacheKey = $"{SchemaIdPrefix}{subject}:{schemaHash}";
		var schemaCacheKey = $"{SchemaByIdPrefix}{schemaId}";

		var cacheOptions = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = _options.CacheDuration,
			Size = 1
		};

		_ = _cache.Set(schemaIdCacheKey, schemaId, cacheOptions);

		var schemaCacheOptions = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = _options.CacheDuration,
			Size = 1 + (schema.Length / 1000)
		};

		_ = _cache.Set(schemaCacheKey, schema, schemaCacheOptions);

		return schemaId;
	}

	/// <inheritdoc/>
	public async Task<bool> IsCompatibleAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken)
	{
		if (!_options.CacheCompatibilityResults)
		{
			return await _inner.IsCompatibleAsync(subject, schema, cancellationToken).ConfigureAwait(false);
		}

		var schemaHash = ComputeSchemaHash(schema);
		var cacheKey = $"{CompatibilityPrefix}{subject}:{schemaHash}";

		if (_cache.TryGetValue(cacheKey, out bool cachedResult))
		{
			LogCacheHit("IsCompatible", subject);
			return cachedResult;
		}

		LogCacheMiss("IsCompatible", subject);
		var isCompatible = await _inner.IsCompatibleAsync(subject, schema, cancellationToken).ConfigureAwait(false);

		var cacheOptions = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = _options.CacheDuration,
			Size = 1
		};

		_ = _cache.Set(cacheKey, isCompatible, cacheOptions);

		return isCompatible;
	}

	/// <summary>
	/// Computes a hash of the schema string for use as a cache key component.
	/// </summary>
	private static string ComputeSchemaHash(string schema)
	{
		var bytes = Encoding.UTF8.GetBytes(schema);
		var hashBytes = SHA256.HashData(bytes);
		return Convert.ToHexString(hashBytes);
	}

	// Source-generated logging methods
	[LoggerMessage(KafkaEventId.SchemaCacheHit, LogLevel.Debug,
		"Cache hit for {Operation} subject {Subject}")]
	private partial void LogCacheHit(string operation, string subject);

	[LoggerMessage(KafkaEventId.SchemaCacheMiss, LogLevel.Debug,
		"Cache miss for {Operation} subject {Subject}")]
	private partial void LogCacheMiss(string operation, string subject);

	[LoggerMessage(KafkaEventId.SchemaCacheHitById, LogLevel.Debug,
		"Cache hit for {Operation} ID {SchemaId}")]
	private partial void LogCacheHitById(string operation, int schemaId);

	[LoggerMessage(KafkaEventId.SchemaCacheMissById, LogLevel.Debug,
		"Cache miss for {Operation} ID {SchemaId}")]
	private partial void LogCacheMissById(string operation, int schemaId);
}
