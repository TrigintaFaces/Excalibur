// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Confluent.Kafka;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for Kafka integration.
/// </summary>
/// <remarks>
/// This type intentionally exposes more than ten properties: it mirrors the native configuration
/// surface of the underlying Kafka client (broker, consumer-group, offset, security, and payload
/// settings) so consumers can tune the transport without reaching for a second options object.
/// The property count reflects the provider's own configuration breadth rather than a design that
/// should be split into narrower types.
/// </remarks>
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
	/// Gets or sets the security protocol used for broker connections.
	/// </summary>
	/// <value>
	/// The security protocol, or <see langword="null"/> to take it from the <c>security.protocol</c>
	/// key in <see cref="AdditionalConfig"/>. When neither supplies one the connection is plaintext.
	/// </value>
	/// <remarks>
	/// <para>
	/// Setting this property and the raw <c>security.protocol</c> key to different values is refused
	/// rather than resolved: a silent winner between two spellings of a security control is how an
	/// intended TLS posture becomes a plaintext connection. Set one or the other.
	/// </para>
	/// <para>
	/// A protocol that does not carry TLS is refused entirely while <see cref="RequireTls"/> is set.
	/// </para>
	/// </remarks>
	public SecurityProtocol? SecurityProtocol { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether broker connections must be encrypted.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to refuse to build any Kafka client whose security protocol does not carry
	/// TLS; <see langword="false"/> to permit an unencrypted connection. Default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// The refusal happens when the client is built, so a misconfigured deployment fails where it is
	/// wired rather than at the first message.
	/// </para>
	/// <para>
	/// <strong>Setting this to false permits credentials and message payloads to travel in the clear.</strong>
	/// It exists for local brokers and test fixtures, not for anything holding real data.
	/// </para>
	/// </remarks>
	public bool RequireTls { get; set; } = true;

	/// <summary>
	/// Gets or sets the consumer tuning options for batching, offset management, and session timeouts.
	/// </summary>
	/// <value>
	/// The consumer tuning options.
	/// </value>
	public KafkaConsumerTuningOptions Consumer { get; set; } = new();

	/// <summary>
	/// Gets additional Kafka consumer configuration properties.
	/// </summary>
	/// <value>
	/// Additional Kafka consumer configuration properties.
	/// </value>
	public Dictionary<string, string> AdditionalConfig { get; } = [];
}

/// <summary>
/// Consumer tuning options for Kafka batching, offset management, and session timeouts.
/// </summary>
public sealed class KafkaConsumerTuningOptions
{
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
	/// Gets or sets the partition assignment strategy for the consumer group. Default is
	/// <see cref="Confluent.Kafka.PartitionAssignmentStrategy.CooperativeSticky"/>, which performs
	/// incremental rebalances so partitions not being reassigned keep consuming during a rebalance.
	/// </summary>
	/// <value>
	/// The partition assignment strategy, or <see langword="null"/> to defer to the broker/client
	/// default. Ignored when <see cref="KafkaOptions.GroupProtocol"/> is the KIP-848
	/// <c>consumer</c> protocol, where assignment is performed server-side.
	/// </value>
	public PartitionAssignmentStrategy? PartitionAssignmentStrategy { get; set; } =
		Confluent.Kafka.PartitionAssignmentStrategy.CooperativeSticky;

	/// <summary>
	/// Gets or sets the maximum inbound-payload length, in bytes, enforced at consume ingress before the
	/// message body is materialized (defense-in-depth DoS hardening). An over-limit message is rejected
	/// before deserialization and skipped (its offset is committed past) rather than stalling the partition.
	/// </summary>
	/// <value>
	/// The maximum payload length in bytes. Default is 4 MiB (bounded by default so the guard is never
	/// inert; Kafka's own limits remain configurable above it). Set to <see langword="null"/> to opt out
	/// (unbounded) for larger legitimate payloads.
	/// </value>
	[Range(1, int.MaxValue)]
	public int? MaxPayloadBytes { get; set; } = PayloadSizeGuard.DefaultMaxPayloadBytes;
}
