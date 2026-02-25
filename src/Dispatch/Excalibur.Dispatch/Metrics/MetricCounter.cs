// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Generic counter wrapper.
/// </summary>
public sealed class MetricCounter<T>(Meter meter, string name, string? unit = null, string? description = null)
	where T : struct
{
	private readonly Counter<T> _counter = meter.CreateCounter<T>(name, unit, description);

	/// <summary>
	/// Adds the specified delta to the counter with optional tags.
	/// </summary>
	/// <param name="delta"> The amount to add to the counter. </param>
	/// <param name="tags"> Optional tags to associate with this measurement. </param>
	public void Add(T delta, TagList tags = default) => _counter.Add(delta, tags);
}
