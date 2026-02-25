// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Google Pub/Sub-specific CloudEvent configuration options.
/// </summary>
public sealed class GooglePubSubCloudEventOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to use ordering keys for message ordering.
	/// </summary>
	/// <remarks> When enabled, CloudEvents will use partition keys as Pub/Sub ordering keys for ordered delivery. </remarks>
	/// <value>
	/// A value indicating whether to use ordering keys for message ordering.
	/// </value>
	public bool UseOrderingKeys { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum message size for Pub/Sub CloudEvents.
	/// </summary>
	/// <remarks> Pub/Sub supports up to 10MB messages. This option allows setting a smaller limit if needed. </remarks>
	/// <value>
	/// The maximum message size for Pub/Sub CloudEvents.
	/// </value>
	public long MaxMessageSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB

	/// <summary>
	/// Gets or sets a value indicating whether to enable message deduplication based on CloudEvent ID.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable message deduplication based on CloudEvent ID.
	/// </value>
	public bool EnableDeduplication { get; set; } = true;

	/// <summary>
	/// Gets or sets the project ID for Pub/Sub operations.
	/// </summary>
	/// <value>
	/// The project ID for Pub/Sub operations.
	/// </value>
	public string? ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the default topic name for publishing CloudEvents.
	/// </summary>
	/// <value>
	/// The default topic name for publishing CloudEvents.
	/// </value>
	public string? DefaultTopic { get; set; }

	/// <summary>
	/// Gets or sets the default subscription name for consuming CloudEvents.
	/// </summary>
	/// <value>
	/// The default subscription name for consuming CloudEvents.
	/// </value>
	public string? DefaultSubscription { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable message compression for large CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable message compression for large CloudEvents.
	/// </value>
	public bool EnableCompression { get; set; }

	/// <summary>
	/// Gets or sets the threshold (in bytes) for triggering message compression.
	/// </summary>
	/// <value>
	/// The threshold (in bytes) for triggering message compression.
	/// </value>
	public int CompressionThreshold { get; set; } = 1024 * 1024; // 1MB

	/// <summary>
	/// Gets or sets a value indicating whether to use exactly-once delivery (requires enabling on the subscription).
	/// </summary>
	/// <value>
	/// A value indicating whether to use exactly-once delivery (requires enabling on the subscription).
	/// </value>
	public bool UseExactlyOnceDelivery { get; set; }

	/// <summary>
	/// Gets or sets the acknowledgment deadline for messages.
	/// </summary>
	/// <value>
	/// The acknowledgment deadline for messages.
	/// </value>
	public TimeSpan AckDeadline { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Gets or sets the retry policy for message publishing.
	/// </summary>
	/// <value>
	/// The retry policy for message publishing.
	/// </value>
	public GooglePubSubRetryPolicy RetryPolicy { get; set; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether to enable Cloud Monitoring integration.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable Cloud Monitoring integration.
	/// </value>
	public bool EnableCloudMonitoring { get; set; }

	/// <summary>
	/// Gets or sets the Cloud Monitoring metric prefix.
	/// </summary>
	/// <value>
	/// The Cloud Monitoring metric prefix.
	/// </value>
	public string CloudMonitoringPrefix { get; set; } = "dispatch.cloudevents";
}
