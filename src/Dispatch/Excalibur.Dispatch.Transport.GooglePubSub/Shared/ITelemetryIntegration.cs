// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Telemetry integration interface.
/// </summary>
public interface ITelemetryIntegration
{
	/// <summary>
	/// Records a metric measurement.
	/// </summary>
	/// <param name="metricName"> The metric name. </param>
	/// <param name="value"> The metric value. </param>
	/// <param name="tags"> Optional tags. </param>
	void RecordMetric(string metricName, double value, IDictionary<string, object?>? tags = null);

	/// <summary>
	/// Starts an activity for tracing.
	/// </summary>
	/// <param name="activityName"> The activity name. </param>
	/// <returns> A disposable activity. </returns>
	IDisposable? StartActivity(string activityName);
}
