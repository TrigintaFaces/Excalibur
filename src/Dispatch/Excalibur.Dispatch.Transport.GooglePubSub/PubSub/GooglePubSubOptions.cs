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
	/// Gets or sets the transport name used for multi-transport routing and DI identity.
	/// </summary>
	/// <value> The transport name; <see langword="null"/> for the default transport. </value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the Google Cloud connection identity (project, topic, subscription).
	/// </summary>
	/// <value> The connection sub-options. Never null. </value>
	public PubSubConnectionOptions Connection { get; set; } = new();
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
	/// Gets the message-type → topic-id routing map used to resolve the destination topic per message type.
	/// </summary>
	/// <value> The routing map keyed by message CLR type. Never null. </value>
	public Dictionary<Type, string> TopicMappings { get; } = new();
}

/// <summary>
/// Google Cloud Pub/Sub connection identity (project, topic, subscription).
/// </summary>
public sealed class PubSubConnectionOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	/// <value>The Google Cloud project ID.</value>
	[Required]
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Pub/Sub topic ID for publishing messages.
	/// </summary>
	/// <value>The Pub/Sub topic ID for publishing messages.</value>
	[Required]
	public string TopicId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Pub/Sub subscription ID for receiving messages.
	/// </summary>
	/// <value>The Pub/Sub subscription ID for receiving messages.</value>
	[Required]
	public string SubscriptionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets the full subscription name in the format projects/{project}/subscriptions/{subscription}.
	/// </summary>
	/// <value>The full subscription name.</value>
	public string SubscriptionName => $"projects/{ProjectId}/subscriptions/{SubscriptionId}";

	/// <summary>
	/// Gets the full topic name in the format projects/{project}/topics/{topic}.
	/// </summary>
	/// <value>The full topic name.</value>
	public string TopicName => $"projects/{ProjectId}/topics/{TopicId}";
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
	/// Gets or sets the maximum inbound payload length, in bytes, enforced at both Pub/Sub ingress surfaces
	/// (receiver and subscriber) before the message body is materialized — a defense-in-depth guard against
	/// allocation denial-of-service from an oversized message. Defaults to 10 MiB (Pub/Sub's own maximum
	/// message size). Set to <see langword="null"/> to opt out of the limit (unbounded).
	/// </summary>
	/// <value> The maximum inbound payload length in bytes, or <see langword="null"/> to opt out. </value>
	[Range(1, int.MaxValue)]
	public int? MaxPayloadBytes { get; set; } = 10 * 1024 * 1024;

	/// <summary>
	/// Gets or sets a value indicating whether the subscription requires per-ordering-key FIFO delivery.
	/// When <see langword="true"/>, the transport validates at startup that the subscription has message
	/// ordering enabled and fails loud if it does not. Default is false.
	/// </summary>
	public bool EnableMessageOrdering { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the subscription requires exactly-once delivery semantics.
	/// When <see langword="true"/>, the transport validates the subscription supports it at startup. Default is false.
	/// </summary>
	public bool EnableExactlyOnceDelivery { get; set; }

	/// <summary>
	/// Gets or sets the streaming-pull flow-control settings (outstanding message/byte bounds).
	/// </summary>
	/// <value> The flow-control sub-options. Never null. </value>
	public PubSubFlowControlOptions FlowControl { get; set; } = new();

	/// <summary>
	/// Gets or sets the dead-letter configuration for messages that exhaust delivery attempts.
	/// </summary>
	/// <value> The dead-letter sub-options. Never null. </value>
	public PubSubDeadLetterOptions DeadLetter { get; set; } = new();
}

/// <summary>
/// Dead-letter configuration for Google Cloud Pub/Sub subscriptions.
/// </summary>
public sealed class PubSubDeadLetterOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable a dead-letter topic on message rejection. Default is false.
	/// </summary>
	public bool Enable { get; set; }

	/// <summary>
	/// Gets or sets the dead-letter topic ID applied when <see cref="Enable"/> is <see langword="true"/>.
	/// </summary>
	public string? TopicId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the transport auto-applies the configured dead-letter policy
	/// to its subscription at startup (a <c>GetSubscription</c> + <c>UpdateSubscription</c> so the policy is
	/// actually attached rather than configured-but-unhonored). Default is false — subscription provisioning
	/// is usually owned by infrastructure-as-code, so the policy is only auto-mutated when opted in.
	/// </summary>
	public bool AutoApplyPolicy { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of delivery attempts before a message is dead-lettered when
	/// <see cref="AutoApplyPolicy"/> is enabled. Default is 5.
	/// </summary>
	public int MaxDeliveryAttempts { get; set; } = 5;
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
	/// Gets or sets custom resource labels for telemetry.
	/// </summary>
	public Dictionary<string, string> TelemetryResourceLabels { get; set; } = [];

}
