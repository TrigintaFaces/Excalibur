// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for real-time security monitoring of Elasticsearch operations including threat detection, anomaly analysis, and
/// automated security response capabilities.
/// </summary>
public interface IElasticsearchSecurityMonitor
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
	/// Gets the current security monitoring configuration settings.
	/// </summary>
	/// <value> The active security monitoring configuration. </value>
	SecurityMonitoringOptions Configuration { get; }

	/// <summary>
	/// Gets a value indicating whether real-time monitoring is currently active.
	/// </summary>
	/// <value> True if security monitoring is running, false otherwise. </value>
	bool IsMonitoring { get; }

	/// <summary>
	/// Gets the supported threat detection capabilities.
	/// </summary>
	/// <value> A collection of threat types that can be detected by this monitor. </value>
	IReadOnlyCollection<ThreatType> SupportedThreatTypes { get; }

	/// <summary>
	/// Gets a value indicating whether automated threat response is enabled.
	/// </summary>
	/// <value> True if automated responses are configured and active, false otherwise. </value>
	bool AutomatedResponseEnabled { get; }

	/// <summary>
	/// Monitors a security event for compliance and threat detection.
	/// </summary>
	/// <param name="securityEvent"> The security event to monitor. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous monitoring operation. </returns>
	Task MonitorSecurityEventAsync(SecurityMonitoringEvent securityEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Starts real-time security monitoring of Elasticsearch operations and connections.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if monitoring was started successfully, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when monitoring startup fails due to security constraints. </exception>
	Task<bool> StartMonitoringAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops real-time security monitoring and releases associated resources.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if monitoring was stopped successfully, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when monitoring shutdown fails due to security constraints. </exception>
	Task<bool> StopMonitoringAsync(CancellationToken cancellationToken);

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

	/// <summary>
	/// Processes pending security alerts and triggers appropriate automated responses.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the number of processed alerts. </returns>
	/// <exception cref="SecurityException"> Thrown when alert processing fails due to security constraints. </exception>
	Task<int> ProcessSecurityAlertsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Analyzes a data access event for unusual patterns and potential data exfiltration attempts.
	/// </summary>
	/// <param name="dataAccessEvent"> The data access event to analyze. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the security analysis result including anomaly
	/// detection and risk assessment.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when security analysis fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the data access event is null. </exception>
	Task<SecurityAnalysisResult>
		AnalyzeDataAccessEventAsync(DataAccessEvent dataAccessEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Performs comprehensive threat detection analysis based on current security events and patterns.
	/// </summary>
	/// <param name="analysisRequest"> The threat detection analysis request with parameters. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the threat detection result including identified
	/// threats and recommended countermeasures.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when threat detection fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the analysis request is null. </exception>
	Task<ThreatDetectionResult> PerformThreatDetectionAsync(
		ThreatDetectionRequest analysisRequest,
		CancellationToken cancellationToken);

	/// <summary>
	/// Calculates the current security risk score based on recent events and system state.
	/// </summary>
	/// <param name="riskCalculationRequest"> The risk calculation request with parameters. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the calculated risk score and contributing factors.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when risk calculation fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the risk calculation request is null. </exception>
	Task<SecurityRiskScore> CalculateSecurityRiskAsync(
		RiskCalculationRequest riskCalculationRequest,
		CancellationToken cancellationToken);

	/// <summary>
	/// Generates security alerts based on detected threats and anomalies.
	/// </summary>
	/// <param name="alertRequest"> The security alert generation request with criteria. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the generated security alerts and their distribution status.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when alert generation fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the alert request is null. </exception>
	Task<SecurityAlertResult> GenerateSecurityAlertsAsync(SecurityAlertRequest alertRequest, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the current security monitoring status and health information.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the monitoring status including active monitors and
	/// health indicators.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when status retrieval fails due to security constraints. </exception>
	Task<SecurityMonitoringStatus> GetMonitoringStatusAsync(CancellationToken cancellationToken);

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

	/// <summary>
	/// Configures automatic security response actions for specific threat types.
	/// </summary>
	/// <param name="responseConfiguration"> The automated response configuration. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the response configuration was applied
	/// successfully, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when response configuration fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the response configuration is null. </exception>
	Task<bool> ConfigureAutomatedResponseAsync(
		AutomatedSecurityResponse responseConfiguration,
		CancellationToken cancellationToken);
}
