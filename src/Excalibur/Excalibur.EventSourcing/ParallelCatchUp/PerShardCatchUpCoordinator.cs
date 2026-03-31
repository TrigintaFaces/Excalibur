// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// Coordinates parallel catch-up processing by assigning one worker per tenant shard.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="CatchUpStrategy.PerShard"/> is active, each registered shard gets its own
/// worker that processes that shard's global event stream independently. There are no
/// cross-shard ordering guarantees.
/// </para>
/// <para>
/// Requires <see cref="ITenantShardMap"/> and <see cref="ITenantStoreResolver{TStore}"/>
/// for <see cref="IRangeQueryableEventStore"/> to be registered in DI.
/// </para>
/// </remarks>
internal sealed class PerShardCatchUpCoordinator
{
	private readonly ITenantShardMap _shardMap;
	private readonly ITenantStoreResolver<IRangeQueryableEventStore> _storeResolver;
	private readonly IParallelCheckpointStore _checkpointStore;
	private readonly ParallelCatchUpOptions _options;
	private readonly ILogger _logger;

	internal PerShardCatchUpCoordinator(
		ITenantShardMap shardMap,
		ITenantStoreResolver<IRangeQueryableEventStore> storeResolver,
		IParallelCheckpointStore checkpointStore,
		ParallelCatchUpOptions options,
		ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(shardMap);
		ArgumentNullException.ThrowIfNull(storeResolver);
		ArgumentNullException.ThrowIfNull(checkpointStore);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_shardMap = shardMap;
		_storeResolver = storeResolver;
		_checkpointStore = checkpointStore;
		_options = options;
		_logger = logger;
	}

	/// <summary>
	/// Runs parallel catch-up with one worker per shard.
	/// </summary>
	/// <param name="projectionName">The projection being caught up.</param>
	/// <param name="fromPosition">The global starting position.</param>
	/// <param name="toPosition">The global ending position.</param>
	/// <param name="applyEvent">Delegate to apply each event to projections.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Total events processed across all shards.</returns>
	internal async Task<long> RunAsync(
		string projectionName,
		long fromPosition,
		long toPosition,
		Func<StoredEvent, CancellationToken, Task> applyEvent,
		CancellationToken cancellationToken)
	{
		var shardIds = _shardMap.GetRegisteredShardIds();

		if (shardIds.Count == 0)
		{
			_logger.WorkerProcessingRange(0, fromPosition, toPosition);
			return 0;
		}

		var workers = new Task<long>[shardIds.Count];
		var workerId = 0;

		foreach (var shardId in shardIds)
		{
			var id = workerId;
			var eventStore = _storeResolver.Resolve(shardId);
			var range = new StreamRange(fromPosition, toPosition);

			var worker = new ParallelCatchUpWorker(
				id,
				range,
				eventStore,
				_checkpointStore,
				$"{projectionName}:{shardId}",
				_options.BatchSize,
				_options.CheckpointInterval,
				_logger);

			workers[workerId] = worker.ProcessAsync(applyEvent, cancellationToken);
			workerId++;
		}

		var results = await Task.WhenAll(workers).ConfigureAwait(false);
		return results.Sum();
	}
}
