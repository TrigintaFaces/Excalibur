// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for dead letter queue handling.
/// </summary>
public sealed class DeadLetterOptions
{
	/// <summary>
	/// Gets or sets the dead letter topic name.
	/// </summary>
	/// <value>
	/// The dead letter topic name.
	/// </value>
	public TopicName? DeadLetterTopicName { get; set; }

	/// <summary>
	/// Gets or sets the dead letter subscription name used for reading DLQ messages and statistics.
	/// </summary>
	/// <value>
	/// The dead letter subscription name.
	/// </value>
	public SubscriptionName? DeadLetterSubscriptionName { get; set; }

	/// <summary>
	/// Gets or sets the default maximum delivery attempts before dead lettering.
	/// Default: 5.
	/// </summary>
	/// <value>
	/// The default maximum delivery attempts before dead lettering.
	/// Default: 5.
	/// </value>
	public int DefaultMaxDeliveryAttempts { get; set; } = 5;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to automatically create DLQ topics and subscriptions.
	/// Default: true.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to automatically create DLQ topics and subscriptions.
	/// Default: true.
	/// </value>
	public bool AutoCreateDeadLetterResources { get; set; } = true;

	/// <summary>
	/// Gets or sets the retention duration for dead letter messages.
	/// Default: 7 days.
	/// </summary>
	/// <value>
	/// The retention duration for dead letter messages.
	/// Default: 7 days.
	/// </value>
	public TimeSpan DeadLetterRetentionDuration { get; set; } = TimeSpan.FromDays(7);

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable automatic retry from DLQ.
	/// Default: false.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable automatic retry from DLQ.
	/// Default: false.
	/// </value>
	public bool EnableAutomaticRetry { get; set; }

	/// <summary>
	/// Gets or sets the automatic retry interval.
	/// Default: 1 hour.
	/// </summary>
	/// <value>
	/// The automatic retry interval.
	/// Default: 1 hour.
	/// </value>
	public TimeSpan AutomaticRetryInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the maximum messages to process in automatic retry.
	/// Default: 100.
	/// </summary>
	/// <value>
	/// The maximum messages to process in automatic retry.
	/// Default: 100.
	/// </value>
	public int AutomaticRetryBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable DLQ monitoring.
	/// Default: true.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable DLQ monitoring.
	/// Default: true.
	/// </value>
	public bool EnableMonitoring { get; set; } = true;

	/// <summary>
	/// Gets or sets the monitoring check interval.
	/// Default: 5 minutes.
	/// </summary>
	/// <value>
	/// The monitoring check interval.
	/// Default: 5 minutes.
	/// </value>
	public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the threshold for alerting on DLQ size.
	/// Default: 1000 messages.
	/// </summary>
	/// <value>
	/// The threshold for alerting on DLQ size.
	/// Default: 1000 messages.
	/// </value>
	public int AlertThresholdMessageCount { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the threshold for alerting on message age.
	/// Default: 24 hours.
	/// </summary>
	/// <value>
	/// The threshold for alerting on message age.
	/// Default: 24 hours.
	/// </value>
	public TimeSpan AlertThresholdMessageAge { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets custom dead letter reasons that should skip retry.
	/// </summary>
	/// <value>
	/// Custom dead letter reasons that should skip retry.
	/// </value>
	public HashSet<string> NonRetryableReasons { get; } =
	[
		"INVALID_MESSAGE_FORMAT",
		"UNAUTHORIZED",
		"MESSAGE_TOO_LARGE",
		"UNSUPPORTED_OPERATION",
	];

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to preserve message ordering in DLQ.
	/// Default: false.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to preserve message ordering in DLQ.
	/// Default: false.
	/// </value>
	public bool PreserveMessageOrdering { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable dead letter queue compression.
	/// Default: true.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable dead letter queue compression.
	/// Default: true.
	/// </value>
	public bool EnableCompression { get; set; } = true;

	/// <summary>
	/// Validates the configuration.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	public void Validate()
	{
		if (DefaultMaxDeliveryAttempts < 1)
		{
			throw new InvalidOperationException("DefaultMaxDeliveryAttempts must be at least 1.");
		}

		if (DeadLetterRetentionDuration < TimeSpan.FromMinutes(10))
		{
			throw new InvalidOperationException("DeadLetterRetentionDuration must be at least 10 minutes.");
		}

		if (AutomaticRetryInterval < TimeSpan.FromMinutes(1))
		{
			throw new InvalidOperationException("AutomaticRetryInterval must be at least 1 minute.");
		}

		if (AutomaticRetryBatchSize is < 1 or > 1000)
		{
			throw new InvalidOperationException("AutomaticRetryBatchSize must be between 1 and 1000.");
		}

		if (MonitoringInterval < TimeSpan.FromSeconds(30))
		{
			throw new InvalidOperationException("MonitoringInterval must be at least 30 seconds.");
		}

		if (AlertThresholdMessageCount < 0)
		{
			throw new InvalidOperationException("AlertThresholdMessageCount must be non-negative.");
		}

		if (AlertThresholdMessageAge < TimeSpan.Zero)
		{
			throw new InvalidOperationException("AlertThresholdMessageAge must be non-negative.");
		}
	}
}
