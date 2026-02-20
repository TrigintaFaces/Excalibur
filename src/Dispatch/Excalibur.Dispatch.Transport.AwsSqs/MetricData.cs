// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

// LongPollingConfiguration is now defined in Common.LongPolling.Configuration namespace Use
// Excalibur.Dispatch.Transport.AwsSqs.Common.LongPolling.Configuration.LongPollingConfiguration instead Interface for long polling strategy moved to
// Aws/Common/LongPolling/Abstractions/ILongPollingStrategy.cs Interface for long polling receiver moved to Aws/Common/LongPolling/Abstractions/ILongPollingReceiver.cs

// MetricUnit enum moved to Excalibur.Dispatch.Transport.AwsSqs.Common.Metrics.Abstractions namespace

/// <summary>
/// Metric data for AWS operations.
/// </summary>
public sealed class MetricData
{
	/// <summary>
	/// Gets or sets the metric name.
	/// </summary>
	/// <value>
	/// The metric name.
	/// </value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the metric value.
	/// </summary>
	/// <value>
	/// The metric value.
	/// </value>
	public double Value { get; set; }

	/// <summary>
	/// Gets or sets the metric unit.
	/// </summary>
	/// <value>
	/// The metric unit.
	/// </value>
	public MetricUnit Unit { get; set; }

	/// <summary>
	/// Gets or sets the timestamp.
	/// </summary>
	/// <value>
	/// The timestamp.
	/// </value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the dimensions.
	/// </summary>
	/// <value>
	/// The dimensions.
	/// </value>
	public Dictionary<string, string> Dimensions { get; } = [];
}

// DUPLICATE REMOVED: Vibe AI generated MessageEnvelope class removed - use canonical Excalibur.Dispatch.Channels.MessageEnvelope instead
