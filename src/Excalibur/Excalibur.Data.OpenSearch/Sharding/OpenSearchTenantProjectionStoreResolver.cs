// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

namespace Excalibur.Data.OpenSearch.Sharding;

/// <summary>
/// Resolves <see cref="IProjectionStore{TProjection}"/> instances per tenant shard
/// for OpenSearch, using index-per-tenant routing via <see cref="ShardInfo.IndexPrefix"/>.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
internal sealed class OpenSearchTenantProjectionStoreResolver<TProjection>
	: ITenantStoreResolver<IProjectionStore<TProjection>>
	where TProjection : class
{
	private readonly ITenantShardMap _shardMap;
	private readonly ILoggerFactory _loggerFactory;
	private readonly OpenSearchProjectionStoreOptions _defaultOptions;
	private readonly ConcurrentDictionary<string, IProjectionStore<TProjection>> _storeCache = new(StringComparer.Ordinal);

	internal OpenSearchTenantProjectionStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptionsMonitor<OpenSearchProjectionStoreOptions> defaultOptions)
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
		var indexPrefix = shardInfo.IndexPrefix ?? _defaultOptions.IndexPrefix;
		var nodeUri = shardInfo.ConnectionString ?? _defaultOptions.NodeUri;
		var perShardOptions = new OpenSearchProjectionStoreOptions
		{
			NodeUri = nodeUri,
			IndexPrefix = indexPrefix,
		};

		var optionsMonitor = new StaticOptionsMonitor<OpenSearchProjectionStoreOptions>(perShardOptions);

#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
		var settings = new ConnectionSettings(new Uri(nodeUri));
#pragma warning restore CA2000
		var client = new OpenSearchClient(settings);

		return new OpenSearchProjectionStore<TProjection>(
			client,
			optionsMonitor,
			_loggerFactory.CreateLogger<OpenSearchProjectionStore<TProjection>>());
	}

	private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
	{
		public StaticOptionsMonitor(T value) => CurrentValue = value;
		public T CurrentValue { get; }
		public T Get(string? name) => CurrentValue;
		public IDisposable? OnChange(Action<T, string?> listener) => null;
	}
}
