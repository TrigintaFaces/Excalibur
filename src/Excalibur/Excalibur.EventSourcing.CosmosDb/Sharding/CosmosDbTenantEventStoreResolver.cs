// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using Excalibur.Dispatch;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.CosmosDb.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for Cosmos DB.
/// </summary>
/// <remarks>
/// <para>
/// Each shard gets its own <see cref="CosmosClient"/> and database, routed via
/// <see cref="ShardInfo.DatabaseName"/> or <see cref="ShardInfo.IndexPrefix"/> (container).
/// </para>
/// <para>
/// The resolver constructs those clients itself, so it owns their lifetime: disposing the resolver
/// disposes every store and every client it created, releasing the per-client connection pool and its
/// sockets. A host that never disposed the resolver would hold one Cosmos client per shard open for
/// the life of the process.
/// </para>
/// </remarks>
[NoTenantTerm(TenantConfinement.EstateWide, "Physical-shard readiness performs a bounded native namespace read for an operator; it selects at most one record identifier, discards it, and does not select event payloads or establish tenant confinement.")]
internal sealed class CosmosDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, ITenantShardReadinessProbe, IAsyncDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly TenantShardBindingCache _bindings = new();
	private readonly Func<ShardInfo, ShardReadinessBinding> _bindingFactory;
	private readonly ILoggerFactory _loggerFactory;
	private readonly CosmosDbEventStoreOptions _defaultOptions;
	private readonly ITenantContext _tenantContext;
	private readonly Func<string, CosmosClientOptions, CosmosClient> _clientFactory;


	internal CosmosDbTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<CosmosDbEventStoreOptions> defaultOptions,
		ITenantContext tenantContext)
        : this(shardMap, loggerFactory, defaultOptions, tenantContext,
            static (connection, options) => new CosmosClient(connection, options)) { }

    internal CosmosDbTenantEventStoreResolver(ITenantShardMap shardMap, ILoggerFactory loggerFactory,
        IOptions<CosmosDbEventStoreOptions> defaultOptions, ITenantContext tenantContext,
        Func<string, CosmosClientOptions, CosmosClient> clientFactory)
    {
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(defaultOptions);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
		_tenantContext = tenantContext;

		_shardMap = shardMap;
		_bindingFactory = CreateBinding;
		_loggerFactory = loggerFactory;
		_defaultOptions = defaultOptions.Value;
	}

	/// <inheritdoc />
	public IEventStore Resolve(string tenantId)
	{

		var shardInfo = _shardMap.GetShardInfo(tenantId);
		return GetBinding(shardInfo).Store;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => _bindings.DisposeAsync();

	/// <inheritdoc />
	public Task ProbeShardAsync(string shardId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return GetBinding(_shardMap.GetShardById(shardId)).Probe(cancellationToken);
	}

	private ShardReadinessBinding GetBinding(ShardInfo shardInfo) => _bindings.Get(shardInfo, _bindingFactory);


	private ShardReadinessBinding CreateBinding(ShardInfo shardInfo) => ShardReadinessBinding.Construct(resources =>
	{

		var options = Options.Create(new CosmosDbEventStoreOptions
		{
			DatabaseName = _defaultOptions.DatabaseName,
			EventsContainerName = shardInfo.RequireCoordinate(shardInfo.IndexPrefix, nameof(ShardInfo.IndexPrefix)),
			PartitionKeyPath = _defaultOptions.PartitionKeyPath,
			CreateContainerIfNotExists = _defaultOptions.CreateContainerIfNotExists,
		});

		var validation = new CosmosDbEventStoreOptionsValidator().Validate(null, options.Value);
		if (validation.Failed) { throw new OptionsValidationException(nameof(CosmosDbEventStoreOptions), typeof(CosmosDbEventStoreOptions), validation.Failures); }
		var cosmosClient = resources.Own(_clientFactory(shardInfo.ConnectionString, new CosmosClientOptions
		{
			UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }
		}));

		// A shard may host more than one tenant, so the store still composes the ambient tenant into its
		// key. Routing to a shard is the physical half of confinement; the key is the logical half, and a
		// co-located pair of tenants needs both.
		var container = cosmosClient.GetContainer(options.Value.DatabaseName, options.Value.EventsContainerName);
		var store = new CosmosDbEventStore(
			cosmosClient,
			options,
			_loggerFactory.CreateLogger<CosmosDbEventStore>(),
			_tenantContext);


		return resources.Complete(store, async token =>
		{
			using var iterator = container.GetItemQueryStreamIterator(new QueryDefinition("SELECT TOP 1 VALUE c.id FROM c"),
				requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
			using var response = await iterator.ReadNextAsync(token).ConfigureAwait(false);
			_ = response.EnsureSuccessStatusCode();
		});
	});
}
