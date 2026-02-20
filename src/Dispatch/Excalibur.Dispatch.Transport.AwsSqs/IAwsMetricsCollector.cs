// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Interface for AWS metrics collector.
/// </summary>
public interface IAwsMetricsCollector
{
	/// <summary>
	/// Records a metric.
	/// </summary>
	void RecordMetric(string name, double value, MetricUnit unit = MetricUnit.None);

	/// <summary>
	/// Records a metric with dimensions.
	/// </summary>
	void RecordMetric(string name, double value, MetricUnit unit, Dictionary<string, string> dimensions);

	/// <summary>
	/// Gets the collected metrics.
	/// </summary>
	IReadOnlyList<MetricData> GetMetrics();

	/// <summary>
	/// Clears the collected metrics.
	/// </summary>
	void Clear();
}
