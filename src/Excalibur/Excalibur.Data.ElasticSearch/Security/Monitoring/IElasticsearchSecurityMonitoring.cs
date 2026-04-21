// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for core security monitoring lifecycle operations including
/// starting, stopping, and checking monitoring status.
/// </summary>
public interface IElasticsearchSecurityMonitoring
{
	/// <summary>
	/// Gets a value indicating whether real-time monitoring is currently active.
	/// </summary>
	/// <value> True if security monitoring is running, false otherwise. </value>
	bool IsMonitoring { get; }

	/// <summary>
	/// Gets the current security monitoring configuration settings.
	/// </summary>
	/// <value> The active security monitoring configuration. </value>
	SecurityMonitoringOptions Configuration { get; }

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
	/// Retrieves the current security monitoring status and health information.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the monitoring status including active monitors and
	/// health indicators.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when status retrieval fails due to security constraints. </exception>
	Task<SecurityMonitoringStatus> GetMonitoringStatusAsync(CancellationToken cancellationToken);
}
