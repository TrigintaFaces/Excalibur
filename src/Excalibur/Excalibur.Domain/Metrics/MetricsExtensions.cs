// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Metrics;

/// <summary>
/// Extension methods for IMetrics interface.
/// </summary>
public static class MetricsExtensions
{
	/// <summary>
	/// Increments a counter metric by the specified value.
	/// </summary>
	/// <param name="metrics"> The metrics instance. </param>
	/// <param name="name"> The name of the counter metric. </param>
	/// <param name="value"> The value to increment by (default is 1). </param>
	/// <param name="tags"> Optional tags to associate with the metric. </param>
	public static void IncrementCounter(this IMetrics metrics, string name, long value = 1, params KeyValuePair<string, object>[] tags)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		metrics.RecordCounter(name, value, tags);
	}
}
