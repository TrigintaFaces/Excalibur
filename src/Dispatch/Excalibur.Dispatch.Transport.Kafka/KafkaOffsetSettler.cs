// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Settles a Kafka offset as terminal and commits the position it advances. The single
/// implementation of that step, shared by every Kafka component that settles offsets on a consumer.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KafkaPartitionProgress"/> decides <i>what</i> may be committed; this performs the commit
/// that follows. They are separate because the tracker holds no consumer, and they are one type here
/// because two copies of the store-then-commit sequence would drift, and a settlement path that drifted
/// from its sibling is how a message-loss defect in this package came about.
/// </para>
/// <para>
/// Logs through the caller's <see cref="ILogger"/>, so each caller keeps its own log category.
/// </para>
/// </remarks>
internal sealed partial class KafkaOffsetSettler
{
	private readonly IConsumer<string, byte[]> _consumer;
	private readonly KafkaPartitionProgress _progress;
	private readonly string _source;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaOffsetSettler"/> class.
	/// </summary>
	/// <param name="consumer">The consumer whose offsets are stored and committed.</param>
	/// <param name="progress">The tracker that decides the commit position.</param>
	/// <param name="source">The source name used in log messages.</param>
	/// <param name="logger">The caller's logger.</param>
	public KafkaOffsetSettler(
		IConsumer<string, byte[]> consumer,
		KafkaPartitionProgress progress,
		string source,
		ILogger logger)
	{
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
		_progress = progress ?? throw new ArgumentNullException(nameof(progress));
		_source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Settles an offset as terminal and commits the resulting position, if it moved.
	/// </summary>
	/// <remarks>
	/// The commit offset comes from <see cref="KafkaPartitionProgress"/> and is the lowest offset still owed
	/// (or one past the highest fetched offset when nothing is), never this message's own offset plus one.
	/// The commit is recorded only after the broker accepts it, so a throwing commit is retried by the next
	/// settle.
	/// </remarks>
	/// <param name="partition">The partition the offset belongs to.</param>
	/// <param name="offset">The offset that reached a terminal state.</param>
	/// <param name="generation">The generation the delivery was made under.</param>
	/// <returns>
	/// <see langword="true"/> when the settlement was accepted (whether or not the position moved far enough
	/// to commit); <see langword="false"/> when it was refused because it carries a generation from before
	/// a rebalance, and so belongs to a tenure that no longer owns the partition.
	/// </returns>
	public bool SettleTerminal(TopicPartition partition, long offset, long generation)
	{
		if (!_progress.IsCurrentGeneration(partition, generation))
		{
			LogStaleGenerationSettleIgnored(_logger, _source, partition.Partition.Value, offset);
			return false;
		}

		if (_progress.TrySettle(partition, offset, generation, out var commitOffset))
		{
			var settled = new TopicPartitionOffset(partition, new Offset(commitOffset));

			// Store BEFORE committing, and store the same position we commit. The consumer runs
			// with enable.auto.offset.store=false, so this is the only thing that ever writes the stored
			// position -- which is what makes the argument-less Commit() on partition revocation commit
			// settled work rather than everything Consume happened to hand out.
			//
			// STORING CAN THROW. librdkafka rejects a store for a partition that is no longer in a
			// fetchable state -- Local_State, or Local_UnknownPartition once it has been reassigned -- and
			// the revoke callback can be delivered on a different thread from the one settling, so a revoke
			// landing between TrySettle and here is an ordinary interleaving rather than a contrivance.
			//
			// It is caught HERE, alone, because by this point TrySettle has already mutated the tracker:
			// the offset has left the owed sets and the position has moved past it, with no
			// rollback. A bookkeeping call that cannot be undone must not be able to fail the settlement
			// that already happened. Letting it propagate let a raw KafkaException escape where the contract
			// is a TransportSettlementException, and on the poison path it aborted an entire batch because
			// one message could not be stored.
			//
			// Losing the store is safe: the commit below names an absolute position, and the next settle
			// recomputes the position from what is still owed. What is lost is only the revoke-time backstop
			// until the next settlement writes it again.
			try
			{
				_consumer.StoreOffset(settled);
			}
			catch (KafkaException ex)
			{
				LogOffsetStoreFailed(_logger, _source, partition.Partition.Value, commitOffset, ex);
			}

			_consumer.Commit([settled]);
			_progress.OnCommitted(partition, commitOffset, generation);
			LogPrefixCommitted(_logger, _source, partition.Partition.Value, commitOffset);
		}

		return true;
	}

	[LoggerMessage(KafkaEventId.TransportReceiverPrefixCommitted, LogLevel.Debug,
		"Kafka transport receiver: committed position for {Source} partition {Partition} at offset {CommitOffset}.")]
	private static partial void LogPrefixCommitted(ILogger logger, string source, int partition, long commitOffset);

	[LoggerMessage(KafkaEventId.TransportReceiverStaleGenerationIgnored, LogLevel.Warning,
		"Kafka transport receiver: ignored a settlement for {Source} partition {Partition} offset {Offset} issued under a previous assignment generation; the partition has since been revoked or reassigned.")]
	private static partial void LogStaleGenerationSettleIgnored(ILogger logger, string source, int partition, long offset);

	[LoggerMessage(KafkaEventId.TransportReceiverOffsetStoreFailed, LogLevel.Warning,
		"Kafka transport receiver: could not store the settled offset for {Source} partition {Partition} at {Offset}; the commit still names an absolute position, so the position is recomputed on the next settlement.")]
	private static partial void LogOffsetStoreFailed(ILogger logger, string source, int partition, long offset, Exception exception);
}
