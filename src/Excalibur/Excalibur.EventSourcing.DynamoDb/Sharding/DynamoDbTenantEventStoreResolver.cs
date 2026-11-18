// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DynamoDb.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for DynamoDB.
/// </summary>
/// <remarks>
/// <para>
/// Each shard gets its own <see cref="IAmazonDynamoDB"/> client. Table name prefix
/// is derived from <see cref="ShardInfo.IndexPrefix"/>.
/// </para>
/// <para>
/// The resolver constructs those SDK clients itself, so it owns their lifetime: disposing the
/// resolver disposes every store and every client it created, releasing the per-client connection
/// pool and its sockets. A host that never disposed the resolver would hold one DynamoDB client and
/// one Streams client per shard open for the life of the process.
/// </para>
/// </remarks>
[NoTenantTerm(TenantConfinement.EstateWide, "Physical-shard readiness performs a bounded native namespace read for an operator; it selects at most one record identifier, discards it, and does not select event payloads or establish tenant confinement.")]
internal sealed class DynamoDbTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, ITenantShardReadinessProbe, IAsyncDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly TenantShardBindingCache _bindings = new();
	private readonly Func<ShardInfo, ShardReadinessBinding> _bindingFactory;
	private readonly ILoggerFactory _loggerFactory;
	private readonly DynamoDbEventStoreOptions _defaultOptions;
	private readonly ITenantContext _tenantContext;
	private readonly Func<AmazonDynamoDBConfig, IAmazonDynamoDB> _clientFactory;
	private readonly Func<AmazonDynamoDBStreamsConfig, IAmazonDynamoDBStreams> _streamsFactory;


	internal DynamoDbTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<DynamoDbEventStoreOptions> defaultOptions,
		ITenantContext tenantContext)
        : this(shardMap, loggerFactory, defaultOptions, tenantContext,
            static config => new AmazonDynamoDBClient(config),
            static config => new AmazonDynamoDBStreamsClient(config)) { }

    internal DynamoDbTenantEventStoreResolver(ITenantShardMap shardMap, ILoggerFactory loggerFactory,
        IOptions<DynamoDbEventStoreOptions> defaultOptions, ITenantContext tenantContext,
        Func<AmazonDynamoDBConfig, IAmazonDynamoDB> clientFactory,
        Func<AmazonDynamoDBStreamsConfig, IAmazonDynamoDBStreams> streamsFactory)
    {
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(defaultOptions);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
		_streamsFactory = streamsFactory ?? throw new ArgumentNullException(nameof(streamsFactory));
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
		var tablePrefix = shardInfo.RequireCoordinate(shardInfo.IndexPrefix, nameof(ShardInfo.IndexPrefix));
		var options = Options.Create(new DynamoDbEventStoreOptions
		{
			EventsTableName = $"{tablePrefix}{_defaultOptions.EventsTableName}",
		});

		var validation = new DynamoDbEventStoreOptionsValidator().Validate(null, options.Value);
		if (validation.Failed) { throw new OptionsValidationException(nameof(DynamoDbEventStoreOptions), typeof(DynamoDbEventStoreOptions), validation.Failures); }
		var config = new AmazonDynamoDBConfig();
		if (shardInfo.Region is not null)
		{
			config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(shardInfo.Region);
		}


		var streamsConfig = new AmazonDynamoDBStreamsConfig();
		if (shardInfo.Region is not null)
		{
			streamsConfig.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(shardInfo.Region);
		}

		var client = resources.Own(_clientFactory(config));
		var streamsClient = resources.Own(_streamsFactory(streamsConfig));


		// A shard may host more than one tenant, so the store still composes the ambient tenant into its
		// partition key. Routing to a shard is the physical half of confinement; the key is the logical
		// half, and a co-located pair of tenants needs both.
		var store = new DynamoDbEventStore(
			client,
			streamsClient,
			options,
			_loggerFactory.CreateLogger<DynamoDbEventStore>(),
			_tenantContext);

		var eventsTable = options.Value.EventsTableName;
		var partitionAttribute = options.Value.PartitionKeyAttribute;
		return resources.Complete(store, async token =>
		{
			// A physical key probe uses Query permission, not the broader Scan permission. It does not invent an ambient tenant.
			_ = await client.QueryAsync(new QueryRequest
			{
				TableName = eventsTable, Limit = 1, ConsistentRead = true,
				KeyConditionExpression = "#pk = :probe", ProjectionExpression = "#pk",
				ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = partitionAttribute },
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":probe"] = new() { S = "__excalibur_readiness__" } }
			}, token).ConfigureAwait(false);
		});
	});
}
