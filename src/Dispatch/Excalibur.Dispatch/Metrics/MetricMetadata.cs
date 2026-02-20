// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Metadata about a metric for registration and export.
/// </summary>
public sealed class MetricMetadata(
	int metricId,
	string name,
	string? description,
	string? unit,
	MetricType type,
	params string[]? labelNames)
{
	/// <summary>
	/// Gets the unique identifier for this metric.
	/// </summary>
	/// <value>The current <see cref="MetricId"/> value.</value>
	public int MetricId { get; } = metricId;

	/// <summary>
	/// Gets the name of the metric.
	/// </summary>
	/// <value>
	/// The name of the metric.
	/// </value>
	public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <summary>
	/// Gets the description of the metric.
	/// </summary>
	/// <value>The current <see cref="Description"/> value.</value>
	public string Description { get; } = description ?? string.Empty;

	/// <summary>
	/// Gets the unit of measurement for the metric.
	/// </summary>
	/// <value>The current <see cref="Unit"/> value.</value>
	public string Unit { get; } = unit ?? string.Empty;

	/// <summary>
	/// Gets the type of the metric (Counter, Gauge, etc.).
	/// </summary>
	/// <value>The current <see cref="Type"/> value.</value>
	public MetricType Type { get; } = type;

	/// <summary>
	/// Gets the names of labels associated with this metric.
	/// </summary>
	/// <value>The current <see cref="LabelNames"/> value.</value>
	public string[] LabelNames { get; } = labelNames ?? [];
}
