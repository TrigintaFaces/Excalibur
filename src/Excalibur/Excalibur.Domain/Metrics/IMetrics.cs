// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Metrics;

/// <summary>
/// Provides metrics collection functionality for the application.
/// </summary>
[Obsolete("Use System.Diagnostics.Metrics.Meter with named Counter<T>/Histogram<T> instruments instead. " +
	"This interface duplicates System.Diagnostics.Metrics without added value. " +
	"See https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics for migration guidance. " +
	"This interface will be removed in a future release.")]
public interface IMetrics
{
	/// <summary>
	/// Records a counter metric with the specified name, value, and tags.
	/// </summary>
	/// <param name="name"> The name of the counter metric. </param>
	/// <param name="value"> The value to add to the counter. </param>
	/// <param name="tags"> Optional tags to associate with the metric. </param>
	void RecordCounter(string name, long value, params KeyValuePair<string, object>[] tags);

	/// <summary>
	/// Records a gauge metric with the specified name, value, and tags.
	/// </summary>
	/// <param name="name"> The name of the gauge metric. </param>
	/// <param name="value"> The current value of the gauge. </param>
	/// <param name="tags"> Optional tags to associate with the metric. </param>
	void RecordGauge(string name, double value, params KeyValuePair<string, object>[] tags);

	/// <summary>
	/// Records a histogram metric with the specified name, value, and tags.
	/// </summary>
	/// <param name="name"> The name of the histogram metric. </param>
	/// <param name="value"> The value to record in the histogram. </param>
	/// <param name="tags"> Optional tags to associate with the metric. </param>
	void RecordHistogram(string name, double value, params KeyValuePair<string, object>[] tags);
}
