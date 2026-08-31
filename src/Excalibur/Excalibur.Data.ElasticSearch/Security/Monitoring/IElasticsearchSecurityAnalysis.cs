// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for security event analysis including authentication analysis,
/// data access monitoring, threat detection, and risk assessment.
/// </summary>
public interface IElasticsearchSecurityAnalysis
{
	/// <summary>
	/// Gets the supported threat detection capabilities.
	/// </summary>
	/// <value> A collection of threat types that can be detected by this monitor. </value>
	IReadOnlyCollection<ThreatType> SupportedThreatTypes { get; }

	/// <summary>
	/// Monitors a security event for compliance and threat detection.
	/// </summary>
	/// <param name="securityEvent"> The security event to monitor. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous monitoring operation. </returns>
	Task MonitorSecurityEventAsync(SecurityMonitoringEvent securityEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Analyzes an authentication event for suspicious patterns and security threats.
	/// </summary>
	/// <param name="authenticationEvent"> The authentication event to analyze. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the security analysis result including threat
	/// assessment and recommended actions.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when security analysis fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the authentication event is null. </exception>
	Task<SecurityAnalysisResult> AnalyzeAuthenticationEventAsync(
		AuthenticationEvent authenticationEvent,
		CancellationToken cancellationToken);
}
