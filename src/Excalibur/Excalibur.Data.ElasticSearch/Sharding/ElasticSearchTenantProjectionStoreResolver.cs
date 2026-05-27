// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Sharding;

/// <summary>
/// Resolves <see cref="IProjectionStore{TProjection}"/> instances per tenant shard
/// for Elasticsearch, using index-per-tenant routing via <see cref="ShardInfo.IndexPrefix"/>.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
internal sealed class ElasticSearchTenantProjectionStoreResolver<TProjection>
	: ITenantStoreResolver<IProjectionStore<TProjection>>
	where TProjection : class
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ElasticSearchProjectionStoreOptions _defaultOptions;
	private readonly ConcurrentDictionary<string, IProjectionStore<TProjection>> _storeCache = new(StringComparer.Ordinal);

	internal ElasticSearchTenantProjectionStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptionsMonitor<ElasticSearchProjectionStoreOptions> defaultOptions)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(defaultOptions);

		_shardMap = shardMap;
		_loggerFactory = loggerFactory;
		_defaultOptions = defaultOptions.CurrentValue;
	}

	/// <inheritdoc />
	public IProjectionStore<TProjection> Resolve(string tenantId)
	{
		var shardInfo = _shardMap.GetShardInfo(tenantId);
		return _storeCache.GetOrAdd(shardInfo.ShardId, _ => CreateStore(shardInfo));
	}

	private IProjectionStore<TProjection> CreateStore(ShardInfo shardInfo)
	{
		// Build per-shard options with tenant's index prefix
		var indexPrefix = shardInfo.IndexPrefix ?? _defaultOptions.IndexPrefix;
		var nodeUri = shardInfo.ConnectionString ?? _defaultOptions.NodeUri;
		var perShardOptions = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = nodeUri,
			IndexPrefix = indexPrefix,
		};

		var optionsMonitor = new StaticOptionsMonitor<ElasticSearchProjectionStoreOptions>(perShardOptions);

		var settings = new ElasticsearchClientSettings(new Uri(nodeUri));
		var client = new ElasticsearchClient(settings);

#pragma warning disable IL2091 // Serialization/reflection inherently not AOT-safe
		return new ElasticSearchProjectionStore<TProjection>(
			client,
			optionsMonitor,
			_loggerFactory.CreateLogger<ElasticSearchProjectionStore<TProjection>>());
#pragma warning restore IL2091
	}

#pragma warning disable IL2091 // StaticOptionsMonitor generic type not statically analyzable
	/// <summary>
	/// Simple IOptionsMonitor implementation for per-shard static options.
	/// </summary>
	private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
	{
		public StaticOptionsMonitor(T value) => CurrentValue = value;
		public T CurrentValue { get; }
		public T Get(string? name) => CurrentValue;
		public IDisposable? OnChange(Action<T, string?> listener) => null;
	}
#pragma warning restore IL2091
}
