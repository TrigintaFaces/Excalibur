// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Diagnostics;

/// <summary>
/// High-performance metrics collection interface for recording generic telemetry data.
/// This is the canonical interface for low-level metrics (counters, gauges, histograms, timings).
/// </summary>
public interface IMetricsCollector
{
	/// <summary>
	/// Records a counter metric.
	/// </summary>
	/// <param name="name"> The name of the metric. </param>
	/// <param name="value"> The value to increment by. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	void RecordCounter(string name, long value = 1, params KeyValuePair<string, string>[] tags);

	/// <summary>
	/// Records a gauge metric (point-in-time value).
	/// </summary>
	/// <param name="name"> The name of the metric. </param>
	/// <param name="value"> The current value of the gauge. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	void RecordGauge(string name, double value, params KeyValuePair<string, string>[] tags);

	/// <summary>
	/// Records a histogram metric (distribution of values).
	/// </summary>
	/// <param name="name"> The name of the metric. </param>
	/// <param name="value"> The value to record in the distribution. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	void RecordHistogram(string name, double value, params KeyValuePair<string, string>[] tags);

	/// <summary>
	/// Records a timing metric.
	/// </summary>
	/// <param name="name"> The name of the metric. </param>
	/// <param name="duration"> The duration to record. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	void RecordTiming(string name, TimeSpan duration, params KeyValuePair<string, string>[] tags);

	/// <summary>
	/// Creates a timing scope that automatically records duration when disposed.
	/// </summary>
	/// <param name="name"> The name of the metric. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	/// <returns> A disposable scope that records timing on disposal. </returns>
	IDisposable StartTimer(string name, params KeyValuePair<string, string>[] tags);
}
