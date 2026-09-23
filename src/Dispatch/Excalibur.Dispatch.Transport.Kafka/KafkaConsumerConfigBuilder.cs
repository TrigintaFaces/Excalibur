// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

			// librdkafka defaults this to TRUE, and that default is a message-loss hazard here. With it
			// on, the client stores offset+1 for every message at the moment Consume HANDS IT TO US --
			// before a handler has run. Any commit that reads stored offsets therefore commits a position
			// that was never true, and a rebalance while work is in flight moves the group past messages
			// still inside handlers. Those offsets are never fetched again by any member.
			//
			// Off, the stored position is written only where a message is settled, so it can never be
			// ahead of completed work. That makes an argument-less Commit() correct BY CONSTRUCTION
			// rather than correct only while every call site remembers to pass an explicit offset.
			EnableAutoOffsetStore = false,
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

		// AdditionalConfig is applied last and can set any librdkafka property by name, so it is the one
		// path that could put auto-offset-store back. Refuse rather than silently overriding it: a
		// consumer who asked for this deserves to be told the request is unsupported and why, and a
		// guarantee that a flag can switch off is not a guarantee.
		if (config.EnableAutoOffsetStore is true)
		{
			throw new InvalidOperationException(
				"enable.auto.offset.store must not be enabled for this consumer. librdkafka stores "
				+ "offset+1 for every message when it is handed to the application, before the handler "
				+ "runs, so a commit of stored offsets can move the group past messages that are still "
				+ "being processed; a rebalance at that moment loses them with no error. This transport "
				+ "stores offsets itself at settlement instead. Remove 'enable.auto.offset.store' from "
				+ "AdditionalConfig.");
		}

		return KafkaSecurityPosture.Apply(config, options);
	}

	private static AutoOffsetReset MapAutoOffsetReset(string value) =>
		value switch
		{
			"earliest" => AutoOffsetReset.Earliest,
			"none" => AutoOffsetReset.Error,
			_ => AutoOffsetReset.Latest,
		};
}
