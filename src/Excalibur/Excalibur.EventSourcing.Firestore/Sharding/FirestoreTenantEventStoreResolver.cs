// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Data;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using Excalibur.Dispatch;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Firestore.Sharding;

/// <summary>
/// Resolves <see cref="IEventStore"/> instances per tenant shard for Firestore.
/// </summary>
/// <remarks>
/// Each shard gets its own <see cref="FirestoreDb"/> instance, with the project/database
/// derived from <see cref="ShardInfo.DatabaseName"/>.
/// </remarks>
[NoTenantTerm(TenantConfinement.EstateWide, "Physical-shard readiness performs a bounded native namespace read for an operator; it selects at most one record identifier, discards it, and does not select event payloads or establish tenant confinement.")]
internal sealed class FirestoreTenantEventStoreResolver : ITenantStoreResolver<IEventStore>, ITenantShardReadinessProbe, IAsyncDisposable
{
	private readonly ITenantShardMap _shardMap;
	private readonly TenantShardBindingCache _bindings = new();
	private readonly Func<ShardInfo, ShardReadinessBinding> _bindingFactory;
	private readonly ILoggerFactory _loggerFactory;
	private readonly FirestoreEventStoreOptions _defaultOptions;
	private readonly ITenantContext _tenantContext;

	internal FirestoreTenantEventStoreResolver(
		ITenantShardMap shardMap,
		ILoggerFactory loggerFactory,
		IOptions<FirestoreEventStoreOptions> defaultOptions,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(defaultOptions);
		ArgumentNullException.ThrowIfNull(tenantContext);

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
		var projectId = shardInfo.RequireCoordinate(shardInfo.DatabaseName, nameof(ShardInfo.DatabaseName));

		var options = Options.Create(new FirestoreEventStoreOptions
		{
			ProjectId = projectId,
			EventsCollectionName = _defaultOptions.EventsCollectionName,
		});

		options.Value.Validate();
		ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.EventsCollectionName);
		var db = FirestoreDb.Create(projectId);

		// A shard may host more than one tenant, so the store still composes the ambient tenant into its
		// key. Routing to a shard is the physical half of confinement; the key is the logical half, and a
		// co-located pair of tenants needs both.
		var collection = db.Collection(options.Value.EventsCollectionName);
		var store = new FirestoreEventStore(
			db,
			options,
			_loggerFactory.CreateLogger<FirestoreEventStore>(),
			_tenantContext);

		return resources.Complete(store, async token =>
		{
			_ = await collection.Select(Array.Empty<FieldPath>()).Limit(1).GetSnapshotAsync(token).ConfigureAwait(false);
		});
	});
}
