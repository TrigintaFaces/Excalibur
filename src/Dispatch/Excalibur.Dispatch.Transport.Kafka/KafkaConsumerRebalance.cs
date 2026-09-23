// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Wires consumer-group rebalance handlers onto a <see cref="ConsumerBuilder{TKey,TValue}"/> so the
/// transport commits processed offsets when partitions are revoked, and logs assignment changes.
/// </summary>
/// <remarks>
/// <para>
/// The handlers return no offsets, so librdkafka performs the (incremental, for cooperative
/// protocols) assign/unassign itself. The revoke handler commits the position held by
/// <see cref="KafkaPartitionProgress"/> — the lowest offset still owed — as an EXPLICIT offset per
/// partition, before those partitions move to another member. The next owner therefore resumes at
/// the earliest message this tenure had not finished, rather than past it.
/// </para>
/// <para>
/// <b>Naming the offsets is what makes that claim true of this call.</b> An argument-less
/// <c>Commit()</c> publishes librdkafka's STORED offsets, which are one past every message handed to
/// the application unless <c>enable.auto.offset.store=false</c> and settlement is the sole writer of
/// the store. That is a property of the whole composition, not of the commit: any store written
/// elsewhere, or a background auto-commit, would silently move the group past messages still inside
/// handlers — never fetched again by any member, with no error, no dead-letter and no log. The
/// configuration still sets <c>enable.auto.offset.store=false</c> and refuses
/// <c>enable.auto.commit</c> at startup, so the two defences agree; see
/// <c>KafkaConsumerConfigBuilder.Build</c> and <c>KafkaOptionsValidator</c>.
/// </para>
/// </remarks>
internal static partial class KafkaConsumerRebalance
{
	/// <summary>
	/// Attaches partition-assigned, partition-revoked (commit-on-revoke), and partition-lost handlers
	/// to the supplied consumer builder.
	/// </summary>
	/// <param name="builder">The consumer builder to configure.</param>
	/// <param name="logger">The logger used to record rebalance activity.</param>
	/// <param name="progress">
	/// The progress tracker the receiver settles through. These handlers are the only place that observes
	/// both edges of a rebalance, so they are where the assignment generation is advanced: a revoke
	/// discards the tenure's tracked positions and the following assignment starts a new generation, which
	/// is what makes a receipt minted before the handover unable to settle after it. Confluent.Kafka does
	/// not surface the group generation id, so this locally-observed epoch is the available signal.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/>, <paramref name="logger"/> or <paramref name="progress"/> is null.
	/// </exception>
	public static void Configure(ConsumerBuilder<string, byte[]> builder, ILogger logger, KafkaPartitionProgress progress)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(progress);

		_ = builder.SetPartitionsAssignedHandler((_, partitions) =>
		{
			progress.OnPartitionsAssigned(partitions);
			LogPartitionsAssigned(logger, partitions.Count);
		});

		_ = builder.SetPartitionsRevokedHandler((consumer, partitions) =>
		{
			LogPartitionsRevoked(logger, partitions.Count);

			// Committed BEFORE the tracker is told about the revoke: the positions being published are the
			// tracker's, and OnPartitionsRevoked discards them.
			CommitOnRevoke(consumer, partitions.Select(static p => p.TopicPartition), progress, logger);
			progress.OnPartitionsRevoked(partitions.Select(static p => p.TopicPartition));
		});

		_ = builder.SetPartitionsLostHandler((_, partitions) =>
		{
			// Partitions are already gone (e.g. session timeout) — committing would be rejected, so
			// only record the loss. The new owner resumes from the last committed offset.
			progress.OnPartitionsRevoked(partitions.Select(static p => p.TopicPartition));
			LogPartitionsLost(logger, partitions.Count);
		});
	}

	/// <summary>
	/// Publishes the tracker's position for each partition being revoked, so the next owner resumes at the
	/// lowest offset still owed rather than past work that is still in flight.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The positions come from <see cref="KafkaPartitionProgress"/> and are committed as EXPLICIT offsets.
	/// An argument-less <c>Commit()</c> publishes librdkafka's STORED offsets instead, which is a different
	/// claim: it is only equal to the tracked position while nothing else ever writes the store, so the
	/// guarantee rested on a property of the whole composition rather than on this call. Naming the offsets
	/// here makes the commit say what it means, and it cannot be defeated by a store written elsewhere.
	/// </para>
	/// <para>
	/// A partition the tracker has no position for is not committed: it has nothing settled to publish, and
	/// committing anything for it would name an offset this tenure never earned.
	/// </para>
	/// </remarks>
	/// <param name="consumer">The consumer whose group position is being published.</param>
	/// <param name="partitions">The partitions being revoked.</param>
	/// <param name="progress">The tracker holding each partition's settled position.</param>
	/// <param name="logger">The logger used to record the outcome.</param>
	internal static void CommitOnRevoke(
		IConsumer<string, byte[]> consumer,
		IEnumerable<TopicPartition> partitions,
		KafkaPartitionProgress progress,
		ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(consumer);
		ArgumentNullException.ThrowIfNull(partitions);
		ArgumentNullException.ThrowIfNull(progress);

		var settled = new List<TopicPartitionOffset>();
		var partitionCount = 0;
		foreach (var partition in partitions)
		{
			partitionCount++;
			if (progress.TryGetPosition(partition, out var position))
			{
				settled.Add(new TopicPartitionOffset(partition, new Offset(position.Value)));
			}
		}

		if (settled.Count == 0)
		{
			// Nothing this tenure settled on any revoked partition. Committing anything here would publish a
			// position no work earned, so the next owner resumes from the last committed offset instead.
			LogCommitOnRevokeNothingSettled(logger, partitionCount, new KafkaException(ErrorCode.Local_NoOffset));
			return;
		}

		try
		{
			consumer.Commit(settled);
			LogCommitOnRevoke(logger, partitionCount);
		}
		catch (KafkaException ex) when (ex.Error.Code == ErrorCode.Local_NoOffset)
		{
			// This used to read "nothing has been consumed/stored yet — benign", and that is no longer
			// what it means. With enable.auto.offset.store=false the store is written only at settlement,
			// so this condition now says THIS TENURE SETTLED NOTHING — which is reachable after consuming
			// thousands of messages, not only on an idle consumer.
			//
			// It is still correct to swallow it: there is genuinely no position to publish, and refusing
			// to commit unsettled work is the whole point of the change. But it is no longer benign, and
			// it is the only place the condition is observable. Handlers slower than the rebalance cadence
			// give: consume, revoke before anything settles, nothing stored, this branch, reassign,
			// consume the same messages, repeat — zero group progress, indefinitely, silently.
			//
			// So it is logged rather than discarded. Trading silent message loss for silent non-progress
			// would be a poor trade; trading it for VISIBLE non-progress is the one worth making.
			LogCommitOnRevokeNothingSettled(logger, partitionCount, ex);
		}
		catch (KafkaException ex)
		{
			LogCommitOnRevokeFailed(logger, ex);
		}
	}

	[LoggerMessage(KafkaEventId.PartitionsAssigned, LogLevel.Information,
		"Kafka consumer: {PartitionCount} partition(s) assigned.")]
	private static partial void LogPartitionsAssigned(ILogger logger, int partitionCount);

	[LoggerMessage(KafkaEventId.PartitionsRevoked, LogLevel.Information,
		"Kafka consumer: {PartitionCount} partition(s) revoked; committing processed offsets.")]
	private static partial void LogPartitionsRevoked(ILogger logger, int partitionCount);

	[LoggerMessage(KafkaEventId.PartitionsLost, LogLevel.Warning,
		"Kafka consumer: {PartitionCount} partition(s) lost (non-graceful revocation); offsets not committed.")]
	private static partial void LogPartitionsLost(ILogger logger, int partitionCount);

	[LoggerMessage(KafkaEventId.CommitOnRevoke, LogLevel.Debug,
		"Kafka consumer: committed offsets for {PartitionCount} revoked partition(s).")]
	private static partial void LogCommitOnRevoke(ILogger logger, int partitionCount);

	[LoggerMessage(KafkaEventId.CommitOnRevokeNothingSettled, LogLevel.Information,
		"Kafka consumer: {PartitionCount} partition(s) revoked with nothing settled by this tenure, so no offset was committed. The messages will be redelivered; if this repeats, handlers are not completing between rebalances and the group is not progressing.")]
	private static partial void LogCommitOnRevokeNothingSettled(ILogger logger, int partitionCount, Exception exception);

	[LoggerMessage(KafkaEventId.CommitOnRevokeFailed, LogLevel.Warning,
		"Kafka consumer: failed to commit offsets during partition revocation.")]
	private static partial void LogCommitOnRevokeFailed(ILogger logger, Exception exception);
}
