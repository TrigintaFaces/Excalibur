// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// protocols) assign/unassign itself. The revoke handler commits the consumer's current offsets
/// before the partitions move to another member, which prevents the next owner from reprocessing
/// already-handled messages after a rebalance.
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
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="logger"/> is null.
	/// </exception>
	public static void Configure(ConsumerBuilder<string, byte[]> builder, ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(logger);

		_ = builder.SetPartitionsAssignedHandler((_, partitions) =>
		{
			LogPartitionsAssigned(logger, partitions.Count);
		});

		_ = builder.SetPartitionsRevokedHandler((consumer, partitions) =>
		{
			LogPartitionsRevoked(logger, partitions.Count);
			CommitOnRevoke(consumer, partitions.Count, logger);
		});

		_ = builder.SetPartitionsLostHandler((_, partitions) =>
		{
			// Partitions are already gone (e.g. session timeout) — committing would be rejected, so
			// only record the loss. The new owner resumes from the last committed offset.
			LogPartitionsLost(logger, partitions.Count);
		});
	}

	private static void CommitOnRevoke(IConsumer<string, byte[]> consumer, int partitionCount, ILogger logger)
	{
		try
		{
			// Commit the consumer's currently stored offsets for the partitions being revoked.
			_ = consumer.Commit();
			LogCommitOnRevoke(logger, partitionCount);
		}
		catch (KafkaException ex) when (ex.Error.Code == ErrorCode.Local_NoOffset)
		{
			// Nothing has been consumed/stored yet — there is no offset to commit. Benign.
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

	[LoggerMessage(KafkaEventId.CommitOnRevokeFailed, LogLevel.Warning,
		"Kafka consumer: failed to commit offsets during partition revocation.")]
	private static partial void LogCommitOnRevokeFailed(ILogger logger, Exception exception);
}
