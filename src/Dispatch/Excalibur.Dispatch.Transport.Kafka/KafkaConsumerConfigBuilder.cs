// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Builds a Confluent <see cref="ConsumerConfig"/> from <see cref="KafkaOptions"/>.
/// </summary>
/// <remarks>
/// Mirrors <see cref="KafkaProducerConfigBuilder"/> for the consume side. The resulting
/// configuration carries the consumer group, manual-commit policy, and session/poll tuning
/// derived from <see cref="KafkaOptions.Consumer"/>, so the transport subscriber consumes from a
/// real broker with the offsets and timeouts the application configured.
/// </remarks>
internal static class KafkaConsumerConfigBuilder
{
	/// <summary>
	/// Builds a <see cref="ConsumerConfig"/> from the supplied <see cref="KafkaOptions"/>.
	/// </summary>
	/// <param name="options">The Kafka connection and consumer tuning options.</param>
	/// <returns>A configured <see cref="ConsumerConfig"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	public static ConsumerConfig Build(KafkaOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var tuning = options.Consumer;

		var config = new ConsumerConfig
		{
			BootstrapServers = options.BootstrapServers,
			GroupId = options.ConsumerGroup,
			EnableAutoCommit = tuning.EnableAutoCommit,
			AutoCommitIntervalMs = tuning.AutoCommitIntervalMs,
			SessionTimeoutMs = tuning.SessionTimeoutMs,
			MaxPollIntervalMs = tuning.MaxPollIntervalMs,
			AutoOffsetReset = MapAutoOffsetReset(tuning.AutoOffsetReset),
			EnablePartitionEof = tuning.EnablePartitionEof,
			QueuedMinMessages = tuning.QueuedMinMessages,
		};

		if (options.GroupProtocol is { } groupProtocol)
		{
			config.GroupProtocol = groupProtocol;
		}

		// partition.assignment.strategy only applies to the classic rebalance protocol. Under the
		// KIP-848 "consumer" group protocol the assignment is server-side and librdkafka rejects the
		// property, so only set it when the classic protocol is in effect.
		if (tuning.PartitionAssignmentStrategy is { } assignmentStrategy
			&& options.GroupProtocol is null or GroupProtocol.Classic)
		{
			config.PartitionAssignmentStrategy = assignmentStrategy;
		}

		foreach (var kvp in options.AdditionalConfig)
		{
			config.Set(kvp.Key, kvp.Value);
		}

		return config;
	}

	private static AutoOffsetReset MapAutoOffsetReset(string value) =>
		value switch
		{
			"earliest" => AutoOffsetReset.Earliest,
			"none" => AutoOffsetReset.Error,
			_ => AutoOffsetReset.Latest,
		};
}
