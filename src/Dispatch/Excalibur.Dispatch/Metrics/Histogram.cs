// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Generic histogram wrapper.
/// </summary>
public sealed class Histogram<T>(Meter meter, string name, string? unit = null, string? description = null)
	where T : struct
{
	private readonly System.Diagnostics.Metrics.Histogram<T> _histogram = meter.CreateHistogram<T>(name, unit, description);

	/// <summary>
	/// Records a measurement value to the histogram with optional tags for categorization. The value will be placed into the appropriate
	/// histogram bucket based on the configured boundaries.
	/// </summary>
	/// <param name="value"> The measurement value to record in the histogram. </param>
	/// <param name="tags"> Optional tags for categorizing the measurement (e.g., by endpoint, method). </param>
	public void Record(T value, TagList tags = default) => _histogram.Record(value, tags);
}
