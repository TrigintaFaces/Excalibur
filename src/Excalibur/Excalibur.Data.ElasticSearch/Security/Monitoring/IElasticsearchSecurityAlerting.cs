// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for security alerting operations including alert processing,
/// risk calculation, alert generation, and automated response configuration.
/// </summary>
public interface IElasticsearchSecurityAlerting
{
	/// <summary>
	/// Gets a value indicating whether automated threat response is enabled.
	/// </summary>
	/// <value> True if automated responses are configured and active, false otherwise. </value>
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
	/// A task that represents the asynchronous operation. The task result contains the generated security alerts and their distribution status.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when alert generation fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the alert request is null. </exception>
	Task<SecurityAlertResult> GenerateSecurityAlertsAsync(SecurityAlertRequest alertRequest, CancellationToken cancellationToken);

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
