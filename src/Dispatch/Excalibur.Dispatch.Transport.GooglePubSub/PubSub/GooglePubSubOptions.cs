// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Google Cloud Pub/Sub integration.
/// </summary>
public sealed class GooglePubSubOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	/// <value>
	/// The Google Cloud project ID.
	/// </value>
	[Required]
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Pub/Sub topic ID for publishing messages.
	/// </summary>
	/// <value>
	/// The Pub/Sub topic ID for publishing messages.
	/// </value>
	[Required]
	public string TopicId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Pub/Sub subscription ID for receiving messages.
	/// </summary>
	/// <value>
	/// The Pub/Sub subscription ID for receiving messages.
	/// </value>
	[Required]
	public string SubscriptionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets the full subscription name in the format projects/{project}/subscriptions/{subscription}.
	/// </summary>
	/// <value>
	/// The full subscription name in the format projects/{project}/subscriptions/{subscription}.
	/// </value>
	public string SubscriptionName => $"projects/{ProjectId}/subscriptions/{SubscriptionId}";

	/// <summary>
	/// Gets the full topic name in the format projects/{project}/topics/{topic}.
	/// </summary>
	/// <value>
	/// The full topic name in the format projects/{project}/topics/{topic}.
	/// </value>
	public string TopicName => $"projects/{ProjectId}/topics/{TopicId}";

	/// <summary>
	/// Gets or sets a value indicating whether enables encryption when sending/receiving messages.
	/// </summary>
	/// <value>
	/// A value indicating whether enables encryption when sending/receiving messages.
	/// </value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of concurrent messages to process in parallel. Default is 0, which means
	/// Environment.ProcessorCount * 2.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent messages to process in parallel. Default is 0, which means
	/// Environment.ProcessorCount * 2.
	/// </value>
	public int MaxConcurrentMessages { get; set; }

	/// <summary>
	/// Gets or sets subscriber configuration (pull, ack, dead letter).
	/// </summary>
	/// <value> The subscriber sub-options. </value>
	public PubSubSubscriberOptions Subscriber { get; set; } = new();

	/// <summary>
	/// Gets or sets telemetry configuration (OpenTelemetry, tracing, Cloud Monitoring).
	/// </summary>
	/// <value> The telemetry sub-options. </value>
	public PubSubTelemetryOptions Telemetry { get; set; } = new();

	/// <summary>
	/// Gets the compression options for Pub/Sub message payloads.
	/// </summary>
	/// <value>
	/// The compression options. Never null.
	/// </value>
	/// <remarks>
	/// <para>
	/// Use compression to reduce message size and bandwidth costs. Snappy is recommended
	/// for high-throughput scenarios (fastest), while Gzip provides better compression ratio.
	/// </para>
	/// <para>
	/// Example:
	/// <code>
	/// options.Compression.Enabled = true;
	/// options.Compression.Algorithm = CompressionAlgorithm.Snappy;
	/// options.Compression.ThresholdBytes = 1024; // Only compress messages > 1KB
	/// </code>
	/// </para>
	/// </remarks>
	public PubSubCompressionOptions Compression { get; } = new();

	// --- Backward-compatible shims ---

	/// <summary>
	/// Gets or sets the maximum number of messages to pull in a single streaming pull request. Default is 100.
	/// </summary>
	[Range(1, 1000)]
	public int MaxPullMessages { get => Subscriber.MaxPullMessages; set => Subscriber.MaxPullMessages = value; }

	/// <summary>
	/// Gets or sets the acknowledgment deadline in seconds for pulled messages. Default is 60 seconds.
	/// </summary>
	[Range(10, 600)]
	public int AckDeadlineSeconds { get => Subscriber.AckDeadlineSeconds; set => Subscriber.AckDeadlineSeconds = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to automatically extend the acknowledgment deadline. Default is true.
	/// </summary>
	public bool EnableAutoAckExtension { get => Subscriber.EnableAutoAckExtension; set => Subscriber.EnableAutoAckExtension = value; }

	/// <summary>
	/// Gets or sets the number of concurrent acknowledgment operations allowed. Default is 10.
	/// </summary>
	[Range(1, 1000)]
	public int MaxConcurrentAcks { get => Subscriber.MaxConcurrentAcks; set => Subscriber.MaxConcurrentAcks = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable dead letter topic on message rejection. Default is false.
	/// </summary>
	public bool EnableDeadLetterTopic { get => Subscriber.EnableDeadLetterTopic; set => Subscriber.EnableDeadLetterTopic = value; }

	/// <summary>
	/// Gets or sets the dead letter topic ID if EnableDeadLetterTopic is true.
	/// </summary>
	public string? DeadLetterTopicId { get => Subscriber.DeadLetterTopicId; set => Subscriber.DeadLetterTopicId = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable OpenTelemetry integration. Default is true.
	/// </summary>
	public bool EnableOpenTelemetry { get => Telemetry.EnableOpenTelemetry; set => Telemetry.EnableOpenTelemetry = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to export custom metrics to Google Cloud Monitoring. Default is false.
	/// </summary>
	public bool ExportToCloudMonitoring { get => Telemetry.ExportToCloudMonitoring; set => Telemetry.ExportToCloudMonitoring = value; }

	/// <summary>
	/// Gets or sets the OTLP endpoint for OpenTelemetry export.
	/// </summary>
	public string? OtlpEndpoint { get => Telemetry.OtlpEndpoint; set => Telemetry.OtlpEndpoint = value; }

	/// <summary>
	/// Gets or sets the interval for exporting telemetry to Cloud Monitoring. Default is 60 seconds.
	/// </summary>
	[Range(10, 3600)]
	public int TelemetryExportIntervalSeconds { get => Telemetry.TelemetryExportIntervalSeconds; set => Telemetry.TelemetryExportIntervalSeconds = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable distributed tracing propagation. Default is true.
	/// </summary>
	public bool EnableTracePropagation { get => Telemetry.EnableTracePropagation; set => Telemetry.EnableTracePropagation = value; }

	/// <summary>
	/// Gets or sets custom resource labels for telemetry.
	/// </summary>
	public Dictionary<string, string> TelemetryResourceLabels { get => Telemetry.TelemetryResourceLabels; set => Telemetry.TelemetryResourceLabels = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to include message attributes in traces. Default is false for privacy.
	/// </summary>
	public bool IncludeMessageAttributesInTraces { get => Telemetry.IncludeMessageAttributesInTraces; set => Telemetry.IncludeMessageAttributesInTraces = value; }

	/// <summary>
	/// Gets or sets the sampling ratio for distributed tracing (0.0 to 1.0). Default is 0.1 (10% sampling).
	/// </summary>
	[Range(0.0, 1.0)]
	public double TracingSamplingRatio { get => Telemetry.TracingSamplingRatio; set => Telemetry.TracingSamplingRatio = value; }
}

/// <summary>
/// Subscriber configuration for Google Cloud Pub/Sub (pull, ack, dead letter).
/// </summary>
public sealed class PubSubSubscriberOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages to pull in a single streaming pull request. Default is 100.
	/// </summary>
	[Range(1, 1000)]
	public int MaxPullMessages { get; set; } = 100;

	/// <summary>
	/// Gets or sets the acknowledgment deadline in seconds for pulled messages. Default is 60 seconds.
	/// </summary>
	[Range(10, 600)]
	public int AckDeadlineSeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically extend the acknowledgment deadline. Default is true.
	/// </summary>
	public bool EnableAutoAckExtension { get; set; } = true;

	/// <summary>
	/// Gets or sets the number of concurrent acknowledgment operations allowed. Default is 10.
	/// </summary>
	[Range(1, 1000)]
	public int MaxConcurrentAcks { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable dead letter topic on message rejection. Default is false.
	/// </summary>
	public bool EnableDeadLetterTopic { get; set; }

	/// <summary>
	/// Gets or sets the dead letter topic ID if EnableDeadLetterTopic is true.
	/// </summary>
	public string? DeadLetterTopicId { get; set; }
}

/// <summary>
/// Telemetry configuration for Google Cloud Pub/Sub (OpenTelemetry, tracing, Cloud Monitoring).
/// </summary>
public sealed class PubSubTelemetryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable OpenTelemetry integration. Default is true.
	/// </summary>
	public bool EnableOpenTelemetry { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to export custom metrics to Google Cloud Monitoring. Default is false.
	/// </summary>
	public bool ExportToCloudMonitoring { get; set; }

	/// <summary>
	/// Gets or sets the OTLP endpoint for OpenTelemetry export. Default is null (uses http://localhost:4317).
	/// </summary>
	public string? OtlpEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the interval for exporting telemetry to Cloud Monitoring. Default is 60 seconds.
	/// </summary>
	[Range(10, 3600)]
	public int TelemetryExportIntervalSeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets a value indicating whether to enable distributed tracing propagation. Default is true.
	/// </summary>
	public bool EnableTracePropagation { get; set; } = true;

	/// <summary>
	/// Gets or sets custom resource labels for telemetry.
	/// </summary>
	public Dictionary<string, string> TelemetryResourceLabels { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to include message attributes in traces. Default is false for privacy.
	/// </summary>
	public bool IncludeMessageAttributesInTraces { get; set; }

	/// <summary>
	/// Gets or sets the sampling ratio for distributed tracing (0.0 to 1.0). Default is 0.1 (10% sampling).
	/// </summary>
	[Range(0.0, 1.0)]
	public double TracingSamplingRatio { get; set; } = 0.1;
}
