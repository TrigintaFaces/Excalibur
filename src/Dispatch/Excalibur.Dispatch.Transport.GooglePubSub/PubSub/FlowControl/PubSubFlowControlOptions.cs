// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Google Cloud Pub/Sub flow control to optimize message throughput and prevent resource exhaustion.
/// </summary>
public sealed class PubSubFlowControlOptions
{
	/// <summary>
	/// Gets or sets the maximum number of outstanding messages that can be held in memory before the subscriber stops pulling
	/// additional messages.
	/// Default: 1000 messages.
	/// </summary>
	/// <value>
	/// The maximum number of outstanding messages that can be held in memory before the subscriber stops pulling
	/// additional messages.
	/// Default: 1000 messages.
	/// </value>
	public int MaxOutstandingElementCount { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum total size (in bytes) of outstanding messages that can be held in memory before the subscriber stops
	/// pulling additional messages.
	/// Default: 100MB (104,857,600 bytes).
	/// </summary>
	/// <value>
	/// The maximum total size (in bytes) of outstanding messages that can be held in memory before the subscriber stops
	/// pulling additional messages.
	/// Default: 100MB (104,857,600 bytes).
	/// </value>
	public long MaxOutstandingByteCount { get; set; } = 100_000_000; // 100MB

	/// <summary>
	/// Gets or sets a value indicating whether adaptive flow control should be enabled. When enabled, flow control limits are
	/// dynamically adjusted based on processing speed and system resources.
	/// Default: true.
	/// </summary>
	/// <value>
	/// A value indicating whether adaptive flow control should be enabled. When enabled, flow control limits are
	/// dynamically adjusted based on processing speed and system resources.
	/// Default: true.
	/// </value>
	public bool EnableAdaptiveFlowControl { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval at which flow control parameters are re-evaluated and potentially adjusted when adaptive flow control
	/// is enabled.
	/// Default: 5 seconds.
	/// </summary>
	/// <value>
	/// The interval at which flow control parameters are re-evaluated and potentially adjusted when adaptive flow control
	/// is enabled.
	/// Default: 5 seconds.
	/// </value>
	public TimeSpan AdaptationInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the minimum number of outstanding messages to maintain. This prevents the flow control from being too restrictive.
	/// Default: 100 messages.
	/// </summary>
	/// <value>
	/// The minimum number of outstanding messages to maintain. This prevents the flow control from being too restrictive.
	/// Default: 100 messages.
	/// </value>
	public int MinOutstandingElementCount { get; set; } = 100;

	/// <summary>
	/// Gets or sets the minimum total size (in bytes) of outstanding messages to maintain.
	/// Default: 10MB (10,485,760 bytes).
	/// </summary>
	/// <value>
	/// The minimum total size (in bytes) of outstanding messages to maintain.
	/// Default: 10MB (10,485,760 bytes).
	/// </value>
	public long MinOutstandingByteCount { get; set; } = 10_000_000; // 10MB

	/// <summary>
	/// Gets or sets the factor by which to increase flow control limits when processing is keeping up with the message rate.
	/// Default: 1.5 (50% increase).
	/// </summary>
	/// <value>
	/// The factor by which to increase flow control limits when processing is keeping up with the message rate.
	/// Default: 1.5 (50% increase).
	/// </value>
	public double ScaleUpFactor { get; set; } = 1.5;

	/// <summary>
	/// Gets or sets the factor by which to decrease flow control limits when processing is falling behind.
	/// Default: 0.8 (20% decrease).
	/// </summary>
	/// <value>
	/// The factor by which to decrease flow control limits when processing is falling behind.
	/// Default: 0.8 (20% decrease).
	/// </value>
	public double ScaleDownFactor { get; set; } = 0.8;

	/// <summary>
	/// Gets or sets the target utilization percentage for flow control. When message queue utilization exceeds this threshold, flow
	/// control will scale down.
	/// Default: 80%.
	/// </summary>
	/// <value>
	/// The target utilization percentage for flow control. When message queue utilization exceeds this threshold, flow
	/// control will scale down.
	/// Default: 80%.
	/// </value>
	public double TargetUtilizationPercentage { get; set; } = 80.0;

	/// <summary>
	/// Gets or sets the memory pressure threshold (as a percentage of available memory) beyond which flow control will become more restrictive.
	/// Default: 75%.
	/// </summary>
	/// <value>
	/// The memory pressure threshold (as a percentage of available memory) beyond which flow control will become more restrictive.
	/// Default: 75%.
	/// </value>
	public double MemoryPressureThreshold { get; set; } = 75.0;

	/// <summary>
	/// Validates the flow control options to ensure they are within acceptable ranges.
	/// </summary>
	/// <exception cref="ArgumentException"> Thrown when any option is invalid. </exception>
	public void Validate()
	{
		if (MaxOutstandingElementCount <= 0)
		{
			throw new ArgumentException("MaxOutstandingElementCount must be greater than 0.", nameof(MaxOutstandingElementCount));
		}

		if (MaxOutstandingByteCount <= 0)
		{
			throw new ArgumentException("MaxOutstandingByteCount must be greater than 0.", nameof(MaxOutstandingByteCount));
		}

		if (MinOutstandingElementCount <= 0)
		{
			throw new ArgumentException("MinOutstandingElementCount must be greater than 0.", nameof(MinOutstandingElementCount));
		}

		if (MinOutstandingByteCount <= 0)
		{
			throw new ArgumentException("MinOutstandingByteCount must be greater than 0.", nameof(MinOutstandingByteCount));
		}

		if (MinOutstandingElementCount > MaxOutstandingElementCount)
		{
			throw new ArgumentException("MinOutstandingElementCount cannot be greater than MaxOutstandingElementCount.");
		}

		if (MinOutstandingByteCount > MaxOutstandingByteCount)
		{
			throw new ArgumentException("MinOutstandingByteCount cannot be greater than MaxOutstandingByteCount.");
		}

		if (AdaptationInterval <= TimeSpan.Zero)
		{
			throw new ArgumentException("AdaptationInterval must be greater than zero.", nameof(AdaptationInterval));
		}

		if (ScaleUpFactor <= 1.0)
		{
			throw new ArgumentException("ScaleUpFactor must be greater than 1.0.", nameof(ScaleUpFactor));
		}

		if (ScaleDownFactor is <= 0.0 or >= 1.0)
		{
			throw new ArgumentException("ScaleDownFactor must be between 0.0 and 1.0.", nameof(ScaleDownFactor));
		}

		if (TargetUtilizationPercentage is <= 0.0 or > 100.0)
		{
			throw new ArgumentException("TargetUtilizationPercentage must be between 0.0 and 100.0.", nameof(TargetUtilizationPercentage));
		}

		if (MemoryPressureThreshold is <= 0.0 or > 100.0)
		{
			throw new ArgumentException("MemoryPressureThreshold must be between 0.0 and 100.0.", nameof(MemoryPressureThreshold));
		}
	}
}
