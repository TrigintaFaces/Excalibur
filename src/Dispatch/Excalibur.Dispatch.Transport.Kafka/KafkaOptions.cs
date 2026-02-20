// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Confluent.Kafka;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for Kafka integration.
/// </summary>
public sealed class KafkaOptions
{
	/// <summary>
	/// Gets or sets the Kafka topic name.
	/// </summary>
	/// <value>
	/// The Kafka topic name.
	/// </value>
	public string Topic { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Kafka bootstrap servers (comma-separated list).
	/// </summary>
	/// <value>
	/// The Kafka bootstrap servers (comma-separated list).
	/// </value>
	[Required]
	public string BootstrapServers { get; set; } = "localhost:9092";

	/// <summary>
	/// Gets or sets the consumer group ID.
	/// </summary>
	/// <value>
	/// The consumer group ID.
	/// </value>
	public string ConsumerGroup { get; set; } = "dispatch-consumer";

	/// <summary>
	/// Gets or sets the consumer group protocol. Default is classic.
	/// </summary>
	/// <value>
	/// The consumer group protocol (classic or consumer). The consumer protocol
	/// enables KIP-848 behavior.
	/// </value>
	public GroupProtocol? GroupProtocol { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether enables encryption when sending/receiving messages.
	/// </summary>
	/// <value>
	/// A value indicating whether enables encryption when sending/receiving messages.
	/// </value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of messages to consume in a single batch. Default is 100.
	/// </summary>
	/// <value>
	/// The maximum number of messages to consume in a single batch. Default is 100.
	/// </value>
	[Range(1, 10000)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum time to wait for a batch to fill in milliseconds. Default is 1000ms (1 second).
	/// </summary>
	/// <value>
	/// The maximum time to wait for a batch to fill in milliseconds. Default is 1000ms (1 second).
	/// </value>
	[Range(1, 60000)]
	public int MaxBatchWaitMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically commit offsets. Default is false (manual commits for better control).
	/// </summary>
	/// <value>
	/// A value indicating whether to automatically commit offsets. Default is false (manual commits for better control).
	/// </value>
	public bool EnableAutoCommit { get; set; }

	/// <summary>
	/// Gets or sets the auto-commit interval in milliseconds if EnableAutoCommit is true. Default is 5000ms (5 seconds).
	/// </summary>
	/// <value>
	/// The auto-commit interval in milliseconds if EnableAutoCommit is true. Default is 5000ms (5 seconds).
	/// </value>
	[Range(100, 300000)]
	public int AutoCommitIntervalMs { get; set; } = 5000;

	/// <summary>
	/// Gets or sets the session timeout in milliseconds. Default is 30000ms (30 seconds).
	/// </summary>
	/// <value>
	/// The session timeout in milliseconds. Default is 30000ms (30 seconds).
	/// </value>
	[Range(1000, 300000)]
	public int SessionTimeoutMs { get; set; } = 30000;

	/// <summary>
	/// Gets or sets the maximum poll interval in milliseconds. Default is 300000ms (5 minutes).
	/// </summary>
	/// <value>
	/// The maximum poll interval in milliseconds. Default is 300000ms (5 minutes).
	/// </value>
	[Range(10000, 1800000)]
	public int MaxPollIntervalMs { get; set; } = 300000;

	/// <summary>
	/// Gets or sets the auto offset reset policy. Valid values: "earliest", "latest", "none". Default is "latest".
	/// </summary>
	/// <value>
	/// The auto offset reset policy. Valid values: "earliest", "latest", "none". Default is "latest".
	/// </value>
	public string AutoOffsetReset { get; set; } = "latest";

	/// <summary>
	/// Gets or sets a value indicating whether to enable partition EOF detection. Default is false.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable partition EOF detection. Default is false.
	/// </value>
	public bool EnablePartitionEof { get; set; }

	/// <summary>
	/// Gets or sets the number of messages to prefetch per partition. Default is 1000.
	/// </summary>
	/// <value>
	/// The number of messages to prefetch per partition. Default is 1000.
	/// </value>
	[Range(1, 100000)]
	public int QueuedMinMessages { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum number of concurrent offset commits. Default is 10.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent offset commits. Default is 10.
	/// </value>
	[Range(1, 100)]
	public int MaxConcurrentCommits { get; set; } = 10;

	/// <summary>
	/// Gets additional Kafka consumer configuration properties.
	/// </summary>
	/// <value>
	/// Additional Kafka consumer configuration properties.
	/// </value>
	public Dictionary<string, string> AdditionalConfig { get; } = [];
}
