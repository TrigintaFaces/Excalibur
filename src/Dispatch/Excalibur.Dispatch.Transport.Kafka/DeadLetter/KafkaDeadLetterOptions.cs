// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for Kafka dead letter queue management.
/// </summary>
/// <remarks>
/// <para>
/// Dead letter topics follow the naming convention <c>{original-topic}.dead-letter</c> by default.
/// A dedicated consumer group is used for DLQ processing, separate from the main consumer group.
/// </para>
/// </remarks>
public sealed class KafkaDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the suffix appended to the original topic name to form the DLQ topic name.
	/// </summary>
	/// <value>The DLQ topic suffix. Defaults to <c>.dead-letter</c>.</value>
	public string TopicSuffix { get; set; } = ".dead-letter";

	/// <summary>
	/// Gets or sets the consumer group ID used for consuming from DLQ topics.
	/// </summary>
	/// <value>The DLQ consumer group ID. Defaults to <c>dlq-processor</c>.</value>
	public string ConsumerGroupId { get; set; } = "dlq-processor";

	/// <summary>
	/// Gets or sets the maximum number of delivery attempts before a message is dead lettered.
	/// </summary>
	/// <value>The maximum delivery attempts. Defaults to 5.</value>
	public int MaxDeliveryAttempts { get; set; } = 5;

	/// <summary>
	/// Gets or sets the retention period for messages in the DLQ topic (mapped to Kafka topic config).
	/// </summary>
	/// <value>The DLQ message retention period. Defaults to 14 days.</value>
	public TimeSpan MessageRetentionPeriod { get; set; } = TimeSpan.FromDays(14);

	/// <summary>
	/// Gets or sets a value indicating whether to include exception stack traces in DLQ message headers.
	/// </summary>
	/// <value><see langword="true"/> to include stack traces; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool IncludeStackTrace { get; set; } = true;

	/// <summary>
	/// Gets or sets the timeout for produce operations to the DLQ topic.
	/// </summary>
	/// <value>The produce timeout. Defaults to 30 seconds.</value>
	public TimeSpan ProduceTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the timeout for consume operations from the DLQ topic.
	/// </summary>
	/// <value>The consume timeout. Defaults to 5 seconds.</value>
	public TimeSpan ConsumeTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets a value indicating whether to automatically create DLQ topics if they don't exist.
	/// </summary>
	/// <value><see langword="true"/> to auto-create DLQ topics; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool AutoCreateTopics { get; set; } = true;

	/// <summary>
	/// Gets or sets the number of partitions for auto-created DLQ topics.
	/// </summary>
	/// <value>The number of partitions. Defaults to 1.</value>
	public int TopicPartitions { get; set; } = 1;

	/// <summary>
	/// Gets or sets the replication factor for auto-created DLQ topics.
	/// </summary>
	/// <value>The replication factor. Defaults to -1 (broker default).</value>
	public short TopicReplicationFactor { get; set; } = -1;

	/// <summary>
	/// Builds the dead letter topic name for the specified source topic.
	/// </summary>
	/// <param name="sourceTopic"> The original topic name. </param>
	/// <returns> The dead letter topic name. </returns>
	public string GetDeadLetterTopicName(string sourceTopic)
	{
		return string.Concat(sourceTopic, TopicSuffix);
	}
}
