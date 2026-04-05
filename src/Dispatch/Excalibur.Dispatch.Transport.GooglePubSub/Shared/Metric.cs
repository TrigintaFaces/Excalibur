// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Metric wrapper for measurements.
/// </summary>
public sealed class Metric
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
	/// Gets or sets the metric timestamp.
	/// </summary>
	/// <value>
	/// The metric timestamp.
	/// </value>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the metric tags.
	/// </summary>
	/// <value>
	/// The metric tags.
	/// </value>
	public Dictionary<string, object?> Tags { get; set; } = [];
}
