// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for security monitoring events and threat intelligence updates
/// including threat detection, anomaly detection, alert generation, and intelligence feed management.
/// </summary>
public interface IElasticsearchSecurityMonitorEvents
{
	/// <summary>
	/// Occurs when a security threat is detected.
	/// </summary>
	event EventHandler<ThreatDetectedEventArgs>? ThreatDetected;

	/// <summary>
	/// Occurs when an anomaly is detected in security patterns.
	/// </summary>
	event EventHandler<AnomalyDetectedEventArgs>? AnomalyDetected;

	/// <summary>
	/// Occurs when a security alert is generated.
	/// </summary>
	event EventHandler<SecurityAlertGeneratedEventArgs>? SecurityAlertGenerated;

	/// <summary>
	/// Occurs when an automated security response is triggered.
	/// </summary>
	event EventHandler<AutomatedResponseTriggeredEventArgs>? AutomatedResponseTriggered;

	/// <summary>
	/// Updates threat intelligence data from external sources for enhanced detection capabilities.
	/// </summary>
	/// <param name="updateRequest"> The threat intelligence update request with source parameters. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the update result including the number of indicators
	/// updated and any errors encountered.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when threat intelligence update fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the update request is null. </exception>
	Task<ThreatIntelligenceUpdateResult> UpdateThreatIntelligenceAsync(
		ThreatIntelligenceUpdateRequest updateRequest,
		CancellationToken cancellationToken);
}
