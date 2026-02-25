// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Event args for anomaly detection events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AnomalyDetectedEventArgs" /> class.
/// </remarks>
/// <param name="anomalyType"> The type of anomaly that was detected. </param>
/// <param name="description"> The detailed description of the detected anomaly. </param>
public sealed class AnomalyDetectedEventArgs(string anomalyType, string description) : EventArgs
{
	/// <summary>
	/// Gets the type of anomaly that was detected.
	/// </summary>
	/// <value> A string describing the category or type of anomaly detected by the security monitoring system. </value>
	public string AnomalyType { get; } = anomalyType;

	/// <summary>
	/// Gets the detailed description of the detected anomaly.
	/// </summary>
	/// <value> A descriptive message providing context and details about the specific anomaly that was detected. </value>
	public string Description { get; } = description;

	/// <summary>
	/// Gets the timestamp when the anomaly was detected.
	/// </summary>
	/// <value> A DateTimeOffset representing the exact moment when the security monitoring system detected the anomaly. </value>
	public DateTimeOffset DetectedAt { get; } = DateTimeOffset.UtcNow;
}
