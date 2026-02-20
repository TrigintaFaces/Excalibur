// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Represents latency measurements for a specific processing stage.
/// </summary>
public sealed class StageLatency
{

	/// <summary>
	/// Gets or sets the name of the stage.
	/// </summary>
	/// <value>The current <see cref="StageName"/> value.</value>
	public string StageName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the minimum latency in milliseconds.
	/// </summary>
	/// <value>The current <see cref="MinLatencyMs"/> value.</value>
	public double MinLatencyMs { get; set; }

	/// <summary>
	/// Gets or sets the maximum latency in milliseconds.
	/// </summary>
	/// <value>The current <see cref="MaxLatencyMs"/> value.</value>
	public double MaxLatencyMs { get; set; }

	/// <summary>
	/// Gets or sets the average latency in milliseconds.
	/// </summary>
	/// <value>The current <see cref="AverageLatencyMs"/> value.</value>
	public double AverageLatencyMs { get; set; }

	/// <summary>
	/// Gets or sets the median latency in milliseconds.
	/// </summary>
	/// <value>The current <see cref="MedianLatencyMs"/> value.</value>
	public double MedianLatencyMs { get; set; }

	/// <summary>
	/// Gets or sets the 95th percentile latency in milliseconds.
	/// </summary>
	/// <value>The current <see cref="P95LatencyMs"/> value.</value>
	public double P95LatencyMs { get; set; }

	/// <summary>
	/// Gets or sets the 99th percentile latency in milliseconds.
	/// </summary>
	/// <value>The current <see cref="P99LatencyMs"/> value.</value>
	public double P99LatencyMs { get; set; }

	/// <summary>
	/// Gets or sets the number of samples measured.
	/// </summary>
	/// <value>The current <see cref="SampleCount"/> value.</value>
	public long SampleCount { get; set; }

	/// <summary>
	/// Gets or sets the timestamp ticks when the measurement started.
	/// </summary>
	/// <value>The current <see cref="MeasurementStartTimeTicks"/> value.</value>
	public long MeasurementStartTimeTicks { get; set; }

	/// <summary>
	/// Gets or sets the timestamp ticks when the measurement ended.
	/// </summary>
	/// <value>The current <see cref="MeasurementEndTimeTicks"/> value.</value>
	public long MeasurementEndTimeTicks { get; set; }

	/// <summary>
	/// Gets the measurement start time as DateTime for compatibility.
	/// </summary>
	/// <value>
	/// The measurement start time as DateTime for compatibility.
	/// </value>
	public DateTime MeasurementStartTime => new(MeasurementStartTimeTicks, DateTimeKind.Utc);

	/// <summary>
	/// Gets the measurement end time as DateTime for compatibility.
	/// </summary>
	/// <value>
	/// The measurement end time as DateTime for compatibility.
	/// </value>
	public DateTime MeasurementEndTime => new(MeasurementEndTimeTicks, DateTimeKind.Utc);

	/// <summary>
	/// Gets the measurement duration.
	/// </summary>
	/// <value>
	/// The measurement duration.
	/// </value>
	public TimeSpan MeasurementDuration => new(MeasurementEndTimeTicks - MeasurementStartTimeTicks);

	/// <summary>
	/// Gets or sets the high-resolution stopwatch for accurate timing.
	/// </summary>
	/// <value>
	/// The high-resolution stopwatch for accurate timing.
	/// </value>
	internal ValueStopwatch MeasurementStopwatch { get; set; }

	/// <summary>
	/// Creates a new instance with default values.
	/// </summary>
	/// <param name="stageName"> The name of the stage. </param>
	/// <returns> A new StageLatency instance. </returns>
	public static StageLatency Create(string stageName)
	{
		var latency = new StageLatency
		{
			StageName = stageName,
			MeasurementStartTimeTicks = DateTimeOffset.UtcNow.Ticks,
			MeasurementStopwatch = ValueStopwatch.StartNew(),
		};
		return latency;
	}

	/// <summary>
	/// Completes the measurement and calculates elapsed time.
	/// </summary>
	public void CompleteMeasurement()
	{
		if (MeasurementStopwatch.IsActive)
		{
			MeasurementEndTimeTicks = DateTimeOffset.UtcNow.Ticks;
			// Stopwatch is available for external access if needed via _measurementStopwatch.ElapsedMilliseconds
		}
	}
}
