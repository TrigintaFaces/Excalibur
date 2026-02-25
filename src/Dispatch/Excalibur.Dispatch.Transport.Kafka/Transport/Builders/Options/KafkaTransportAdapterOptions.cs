// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for the Kafka transport adapter, aggregating all builder settings.
/// </summary>
/// <remarks>
/// <para>
/// This class serves as the central aggregation point for all Kafka transport configuration.
/// It is populated by the <see cref="IKafkaTransportBuilder"/> fluent API and used during
/// service registration.
/// </para>
/// </remarks>
public sealed class KafkaTransportOptions
{
	/// <summary>
	/// Gets or sets the name of this transport adapter instance.
	/// </summary>
	/// <value>The transport name for multi-transport routing. Default is "kafka".</value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the Kafka bootstrap servers (comma-separated list).
	/// </summary>
	/// <value>The bootstrap servers connection string (e.g., "localhost:9092").</value>
	public string BootstrapServers { get; set; } = "localhost:9092";

	/// <summary>
	/// Gets a value indicating whether schema registry is enabled.
	/// </summary>
	internal bool UseSchemaRegistryEnabled => SchemaRegistry is not null;

	/// <summary>
	/// Gets or sets the schema registry options when enabled.
	/// </summary>
	internal ConfluentSchemaRegistryOptions? SchemaRegistry { get; set; }

	/// <summary>
	/// Gets or sets the producer configuration options.
	/// </summary>
	internal KafkaProducerOptions? ProducerOptions { get; set; }

	/// <summary>
	/// Gets or sets the consumer configuration options.
	/// </summary>
	internal KafkaConsumerOptions? ConsumerOptions { get; set; }

	/// <summary>
	/// Gets the topic mappings by message type.
	/// </summary>
	internal Dictionary<Type, string> TopicMappings { get; } = [];

	/// <summary>
	/// Gets a value indicating whether there are any topic mappings.
	/// </summary>
	internal bool HasTopicMappings => TopicMappings.Count > 0;

	/// <summary>
	/// Gets or sets the topic prefix for auto-generated topic names.
	/// </summary>
	internal string? TopicPrefix { get; set; }

	/// <summary>
	/// Gets or sets the CloudEvents configuration options.
	/// </summary>
	internal KafkaCloudEventOptions? CloudEventOptions { get; set; }
}

/// <summary>
/// Configuration options for the Kafka producer.
/// </summary>
public sealed class KafkaProducerOptions
{
	/// <summary>
	/// Gets or sets the producer client ID.
	/// </summary>
	/// <value>The client ID used to identify the producer to the broker.</value>
	public string ClientId { get; set; } = "dispatch-producer";

	/// <summary>
	/// Gets or sets the acknowledgment level for message delivery.
	/// </summary>
	/// <value>The acks setting for the producer. Default is All.</value>
	public KafkaAckLevel Acks { get; set; } = KafkaAckLevel.All;

	/// <summary>
	/// Gets or sets a value indicating whether to enable idempotent producer.
	/// </summary>
	/// <value>True to enable exactly-once semantics; otherwise, false.</value>
	public bool EnableIdempotence { get; set; } = true;

	/// <summary>
	/// Gets or sets the compression type for messages.
	/// </summary>
	/// <value>The compression algorithm to use.</value>
	public KafkaCompressionType CompressionType { get; set; } = KafkaCompressionType.None;

	/// <summary>
	/// Gets or sets a value indicating whether to enable transactional messaging.
	/// </summary>
	/// <value>True to enable transactions; otherwise, false.</value>
	public bool EnableTransactions { get; set; }

	/// <summary>
	/// Gets or sets the transactional ID for exactly-once processing.
	/// </summary>
	/// <value>The transactional ID, required when EnableTransactions is true.</value>
	public string? TransactionalId { get; set; }

	/// <summary>
	/// Gets or sets the maximum time to wait for a batch to fill.
	/// </summary>
	/// <value>The linger time before sending a batch.</value>
	public TimeSpan LingerMs { get; set; } = TimeSpan.FromMilliseconds(5);

	/// <summary>
	/// Gets or sets the maximum batch size in bytes.
	/// </summary>
	/// <value>The maximum size of a message batch.</value>
	public int BatchSize { get; set; } = 16384;

	/// <summary>
	/// Gets additional producer configuration properties.
	/// </summary>
	public Dictionary<string, string> AdditionalConfig { get; } = [];
}

/// <summary>
/// Configuration options for the Kafka consumer.
/// </summary>
public sealed class KafkaConsumerOptions
{
	/// <summary>
	/// Gets or sets the consumer group ID.
	/// </summary>
	/// <value>The consumer group identifier.</value>
	public string GroupId { get; set; } = "dispatch-consumer";

	/// <summary>
	/// Gets or sets the auto offset reset policy.
	/// </summary>
	/// <value>Where to start consuming when no committed offset exists.</value>
	public KafkaOffsetReset AutoOffsetReset { get; set; } = KafkaOffsetReset.Latest;

	/// <summary>
	/// Gets or sets a value indicating whether to enable auto-commit.
	/// </summary>
	/// <value>True to auto-commit offsets; otherwise, false for manual commits.</value>
	public bool EnableAutoCommit { get; set; }

	/// <summary>
	/// Gets or sets the auto-commit interval.
	/// </summary>
	/// <value>The interval between auto-commits when enabled.</value>
	public TimeSpan AutoCommitInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the session timeout.
	/// </summary>
	/// <value>The timeout for detecting consumer failures.</value>
	public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum poll interval.
	/// </summary>
	/// <value>The maximum time between polls before the consumer is considered failed.</value>
	public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum number of messages to fetch in a single poll.
	/// </summary>
	/// <value>The max batch size for consuming.</value>
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets additional consumer configuration properties.
	/// </summary>
	public Dictionary<string, string> AdditionalConfig { get; } = [];
}
