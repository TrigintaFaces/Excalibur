// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for security alerting operations including alert processing,
/// risk calculation, and alert generation.
/// </summary>
public interface IElasticsearchSecurityAlerting
{
	/// <summary>
	/// Gets a value indicating whether the <c>AutomatedResponseTriggered</c> event is raised for
	/// high-priority alerts. Acting on that event is the consumer's responsibility; this package
	/// raises it and takes no action of its own.
	/// </summary>
	/// <value> True if the event is raised for high-priority alerts, false otherwise. </value>
	bool AutomatedResponseEnabled { get; }

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
	/// Processes pending security alerts and triggers appropriate automated responses.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the number of processed alerts. </returns>
	/// <exception cref="SecurityException"> Thrown when alert processing fails due to security constraints. </exception>
	Task<int> ProcessSecurityAlertsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Generates security alerts based on detected threats and anomalies.
	/// </summary>
	/// <param name="alertRequest"> The security alert generation request with criteria. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the generated security alerts. Alerts are generated and,
	/// when enabled, stored in Elasticsearch and raised on <c>SecurityAlertGenerated</c>; this package does not deliver them to a
	/// notification channel.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when alert generation fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the alert request is null. </exception>
	Task<SecurityAlertResult> GenerateSecurityAlertsAsync(SecurityAlertRequest alertRequest, CancellationToken cancellationToken);
}
