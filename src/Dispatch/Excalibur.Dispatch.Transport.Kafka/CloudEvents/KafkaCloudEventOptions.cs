// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka-specific CloudEvent configuration options.
/// </summary>
public sealed class KafkaCloudEventOptions
{
	/// <summary>
	/// Gets or sets the partitioning strategy for CloudEvents.
	/// </summary>
	/// <value>
	/// The partitioning strategy for CloudEvents.
	/// </value>
	public KafkaPartitioningStrategy PartitioningStrategy { get; set; } = KafkaPartitioningStrategy.CorrelationId;

	/// <summary>
	/// Gets or sets the default topic name for publishing CloudEvents.
	/// </summary>
	/// <value>
	/// The default topic name for publishing CloudEvents.
	/// </value>
	public string? DefaultTopic { get; set; }

	/// <summary>
	/// Gets or sets the number of partitions to create for new topics.
	/// </summary>
	/// <value>
	/// The number of partitions to create for new topics.
	/// </value>
	public int DefaultPartitionCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets the replication factor for new topics.
	/// </summary>
	/// <value>
	/// The replication factor for new topics.
	/// </value>
	public short DefaultReplicationFactor { get; set; } = 1;

	/// <summary>
	/// Gets or sets a value indicating whether to enable idempotent producer for exactly-once semantics.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable idempotent producer for exactly-once semantics.
	/// </value>
	public bool EnableIdempotentProducer { get; set; } = true;

	/// <summary>
	/// Gets or sets the acknowledgment level for message publishing.
	/// </summary>
	/// <remarks>
	/// All: Wait for all in-sync replicas (highest durability)
	/// Leader: Wait for leader replica only (balanced)
	/// None: Fire and forget (highest performance, lowest durability).
	/// </remarks>
	/// <value>
	/// The acknowledgment level for message publishing.
	/// </value>
	public KafkaAckLevel AcknowledgmentLevel { get; set; } = KafkaAckLevel.All;

	/// <summary>
	/// Gets or sets the maximum message size for Kafka CloudEvents.
	/// </summary>
	/// <remarks> Default Kafka broker setting is typically 1MB. Adjust based on your broker configuration. </remarks>
	/// <value>
	/// The maximum message size for Kafka CloudEvents.
	/// </value>
	public int MaxMessageSizeBytes { get; set; } = 1024 * 1024; // 1MB

	/// <summary>
	/// Gets or sets a value indicating whether to enable compression for large CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable compression for large CloudEvents.
	/// </value>
	public bool EnableCompression { get; set; } = true;

	/// <summary>
	/// Gets or sets the compression algorithm to use.
	/// </summary>
	/// <value>
	/// The compression algorithm to use.
	/// </value>
	public KafkaCompressionType CompressionType { get; set; } = KafkaCompressionType.Snappy;

	/// <summary>
	/// Gets or sets the threshold (in bytes) for triggering compression.
	/// </summary>
	/// <value>
	/// The threshold (in bytes) for triggering compression.
	/// </value>
	public int CompressionThreshold { get; set; } = 1024; // 1KB

	/// <summary>
	/// Gets or sets the producer retry settings.
	/// </summary>
	/// <value>
	/// The producer retry settings.
	/// </value>
	public KafkaRetryOptions RetrySettings { get; set; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether to enable transactional messaging.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable transactional messaging.
	/// </value>
	public bool EnableTransactions { get; set; }

	/// <summary>
	/// Gets or sets the transactional ID for exactly-once processing.
	/// </summary>
	/// <value>
	/// The transactional ID for exactly-once processing.
	/// </value>
	public string? TransactionalId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to auto-create topics if they don't exist.
	/// </summary>
	/// <value>
	/// A value indicating whether to auto-create topics if they don't exist.
	/// </value>
	public bool AutoCreateTopics { get; set; }

	/// <summary>
	/// Gets or sets the consumer group ID for CloudEvent consumers.
	/// </summary>
	/// <value>
	/// The consumer group ID for CloudEvent consumers.
	/// </value>
	public string? ConsumerGroupId { get; set; }

	/// <summary>
	/// Gets or sets the offset reset behavior for new consumer groups.
	/// </summary>
	/// <value>
	/// The offset reset behavior for new consumer groups.
	/// </value>
	public KafkaOffsetReset OffsetReset { get; set; } = KafkaOffsetReset.Latest;
}
