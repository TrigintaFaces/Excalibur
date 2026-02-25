// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Interface for transport health checking.
/// </summary>
public interface ITransportHealthChecker
{
	/// <summary>
	/// Gets the name of the health checker.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Gets the transport type being checked.
	/// </summary>
	string TransportType { get; }

	/// <summary>
	/// Gets the health check categories supported by this checker.
	/// </summary>
	TransportHealthCheckCategory Categories { get; }

	/// <summary>
	/// Performs a health check operation.
	/// </summary>
	/// <param name="context">The health check context.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The health check result.</returns>
	Task<TransportHealthCheckResult> CheckHealthAsync(
		TransportHealthCheckContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Performs a quick health check operation (lighter than full check).
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The health check result.</returns>
	Task<TransportHealthCheckResult> CheckQuickHealthAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets health metrics for this transport.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Health metrics.</returns>
	Task<TransportHealthMetrics> GetHealthMetricsAsync(
		CancellationToken cancellationToken);
}
