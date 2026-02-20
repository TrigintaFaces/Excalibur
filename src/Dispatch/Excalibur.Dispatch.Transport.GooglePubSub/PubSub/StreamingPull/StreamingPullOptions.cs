// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Google Pub/Sub streaming pull operations.
/// </summary>
public sealed class StreamingPullOptions
{
	/// <summary>
	/// Gets or sets the number of concurrent streams to maintain.
	/// </summary>
	/// <remarks> Multiple streams can increase throughput by parallelizing message retrieval. Default is 4 streams. </remarks>
	/// <value>
	/// The number of concurrent streams to maintain.
	/// </value>
	[Range(1, 32, ErrorMessage = "ConcurrentStreams must be between 1 and 32")]
	public int ConcurrentStreams { get; set; } = 4;

	/// <summary>
	/// Gets or sets the maximum number of outstanding messages per stream.
	/// </summary>
	/// <remarks>
	/// Controls flow control to prevent overwhelming the examples.AdvancedSample.Consumer. Default is 1000 messages per stream.
	/// </remarks>
	/// <value>
	/// The maximum number of outstanding messages per stream.
	/// </value>
	[Range(10, 10000, ErrorMessage = "MaxOutstandingMessagesPerStream must be between 10 and 10000")]
	public int MaxOutstandingMessagesPerStream { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum bytes of outstanding messages per stream.
	/// </summary>
	/// <remarks> Provides memory-based flow control. Default is 100MB per stream. </remarks>
	/// <value>
	/// The maximum bytes of outstanding messages per stream.
	/// </value>
	[Range(1048576, 1073741824, ErrorMessage = "MaxOutstandingBytesPerStream must be between 1MB and 1GB")]
	public long MaxOutstandingBytesPerStream { get; set; } = 104857600; // 100MB

	/// <summary>
	/// Gets or sets the stream acknowledgment deadline in seconds.
	/// </summary>
	/// <remarks> Time allowed to process a message before Pub/Sub considers it unacknowledged. Default is 60 seconds. </remarks>
	/// <value>
	/// The stream acknowledgment deadline in seconds.
	/// </value>
	[Range(10, 600, ErrorMessage = "StreamAckDeadlineSeconds must be between 10 and 600")]
	public int StreamAckDeadlineSeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets the maximum time to wait for messages before reconnecting.
	/// </summary>
	/// <remarks> Helps detect stale streams. Default is 90 seconds. </remarks>
	/// <value>
	/// The maximum time to wait for messages before reconnecting.
	/// </value>
	public TimeSpan StreamIdleTimeout { get; set; } = TimeSpan.FromSeconds(90);

	/// <summary>
	/// Gets or sets a value indicating whether to automatically extend acknowledgment deadlines.
	/// </summary>
	/// <remarks> When enabled, the system will automatically extend deadlines for messages that are still being processed. </remarks>
	/// <value>
	/// <see langword="true" /> if acknowledgment deadlines are automatically extended; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool AutoExtendAckDeadline { get; set; } = true;

	/// <summary>
	/// Gets or sets the acknowledgment extension threshold.
	/// </summary>
	/// <remarks>
	/// When AutoExtendAckDeadline is true, extends deadline when this percentage of the deadline has elapsed. Default is 80%.
	/// </remarks>
	/// <value>
	/// The acknowledgment extension threshold.
	/// </value>
	[Range(50, 95, ErrorMessage = "AckExtensionThresholdPercent must be between 50 and 95")]
	public int AckExtensionThresholdPercent { get; set; } = 80;

	/// <summary>
	/// Gets or sets the reconnection backoff settings.
	/// </summary>
	/// <value>
	/// The reconnection backoff settings.
	/// </value>
	public BackoffOptions ReconnectionBackoff { get; set; } = new()
	{
		InitialDelay = TimeSpan.FromSeconds(1),
		MaxDelay = TimeSpan.FromSeconds(60),
		Multiplier = 2.0,
		MaxAttempts = 10,
	};

	/// <summary>
	/// Gets or sets monitoring and metrics options for streaming pull operations.
	/// </summary>
	/// <value> The monitoring sub-options. </value>
	public StreamingPullMonitoringOptions Monitoring { get; set; } = new();

	// --- Backward-compatible shims that delegate to sub-options ---

	/// <summary>
	/// Gets or sets a value indicating whether to enable stream health monitoring.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if health monitoring is enabled; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool EnableHealthMonitoring { get => Monitoring.EnableHealthMonitoring; set => Monitoring.EnableHealthMonitoring = value; }

	/// <summary>
	/// Gets or sets the health check interval.
	/// </summary>
	/// <value>
	/// The health check interval. The default is 30 seconds.
	/// </value>
	public TimeSpan HealthCheckInterval { get => Monitoring.HealthCheckInterval; set => Monitoring.HealthCheckInterval = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed stream metrics.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if detailed metrics are enabled; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool EnableDetailedMetrics { get => Monitoring.EnableDetailedMetrics; set => Monitoring.EnableDetailedMetrics = value; }

	/// <summary>
	/// Gets or sets the metrics reporting interval.
	/// </summary>
	/// <value>
	/// The metrics reporting interval. The default is 60 seconds.
	/// </value>
	public TimeSpan MetricsReportingInterval { get => Monitoring.MetricsReportingInterval; set => Monitoring.MetricsReportingInterval = value; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	[RequiresUnreferencedCode("Validation may require unreferenced types for reflection-based validation attributes")]
	public void Validate()
	{
		var validationContext = new ValidationContext(this);
		Validator.ValidateObject(this, validationContext, validateAllProperties: true);

		if (StreamIdleTimeout <= TimeSpan.FromSeconds(StreamAckDeadlineSeconds))
		{
			throw new ArgumentException(
				$"{nameof(StreamIdleTimeout)} ({StreamIdleTimeout}) must be greater than " +
				$"{nameof(StreamAckDeadlineSeconds)} ({StreamAckDeadlineSeconds} seconds)");
		}

		if (HealthCheckInterval >= StreamIdleTimeout)
		{
			throw new ArgumentException(
				$"{nameof(HealthCheckInterval)} ({HealthCheckInterval}) must be less than " +
				$"{nameof(StreamIdleTimeout)} ({StreamIdleTimeout})");
		}
	}
}

/// <summary>
/// Monitoring and metrics options for streaming pull operations.
/// </summary>
public sealed class StreamingPullMonitoringOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable stream health monitoring.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if health monitoring is enabled; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool EnableHealthMonitoring { get; set; } = true;

	/// <summary>
	/// Gets or sets the health check interval.
	/// </summary>
	/// <value>
	/// The health check interval. The default is 30 seconds.
	/// </value>
	public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed stream metrics.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if detailed metrics are enabled; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool EnableDetailedMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets the metrics reporting interval.
	/// </summary>
	/// <value>
	/// The metrics reporting interval. The default is 60 seconds.
	/// </value>
	public TimeSpan MetricsReportingInterval { get; set; } = TimeSpan.FromSeconds(60);
}
