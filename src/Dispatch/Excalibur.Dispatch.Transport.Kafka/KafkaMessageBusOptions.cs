// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for Kafka message bus services.
/// </summary>
public sealed class KafkaMessageBusOptions
{
	/// <summary>
	/// Gets or sets kafka bootstrap servers connection string.
	/// </summary>
	/// <value>
	/// Kafka bootstrap servers connection string.
	/// </value>
	public string? BootstrapServers { get; set; }

	/// <summary>
	/// Gets or sets producer client ID.
	/// </summary>
	/// <value>
	/// Producer client ID.
	/// </value>
	public string? ProducerClientId { get; set; } = "dispatch-producer";

	/// <summary>
	/// Gets or sets consumer group ID.
	/// </summary>
	/// <value>
	/// Consumer group ID.
	/// </value>
	public string? ConsumerGroupId { get; set; } = "dispatch-consumer";

	/// <summary>
	/// Gets or sets a value indicating whether to enable CloudEvents support.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable CloudEvents support.
	/// </value>
	public bool EnableCloudEvents { get; set; } = true;

	/// <summary>
	/// Gets or sets kafka compression type for CloudEvents.
	/// </summary>
	/// <value>
	/// Kafka compression type for CloudEvents.
	/// </value>
	public KafkaCompressionType CompressionType { get; set; } = KafkaCompressionType.None;

	/// <summary>
	/// Gets or sets acknowledgment level for message delivery.
	/// </summary>
	/// <value>
	/// Acknowledgment level for message delivery.
	/// </value>
	public KafkaAckLevel AckLevel { get; set; } = KafkaAckLevel.All;

	/// <summary>
	/// Gets or sets partitioning strategy for message distribution.
	/// </summary>
	/// <value>
	/// Partitioning strategy for message distribution.
	/// </value>
	public KafkaPartitioningStrategy PartitioningStrategy { get; set; } = KafkaPartitioningStrategy.RoundRobin;

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
	/// Gets or sets a value indicating whether to auto-create topics.
	/// </summary>
	/// <value>
	/// A value indicating whether to auto-create topics.
	/// </value>
	public bool AutoCreateTopics { get; set; }

	/// <summary>
	/// Gets or sets the default topic name for publishing.
	/// </summary>
	/// <value>
	/// The default topic name for publishing.
	/// </value>
	public string? DefaultTopic { get; set; }
}
