// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Defines health check capabilities for persistence providers.
/// </summary>
public interface IPersistenceHealthCheck : IHealthCheck
{
	/// <summary>
	/// Gets the name of the health check.
	/// </summary>
	/// <value>
	/// The name of the health check.
	/// </value>
	string HealthCheckName { get; }

	/// <summary>
	/// Gets the tags associated with this health check.
	/// </summary>
	/// <value>
	/// The tags associated with this health check.
	/// </value>
	IEnumerable<string> Tags { get; }

	/// <summary>
	/// Gets or sets the timeout for the health check operation.
	/// </summary>
	/// <value>
	/// The timeout for the health check operation.
	/// </value>
	TimeSpan Timeout { get; set; }

	/// <summary>
	/// Performs a detailed health check including connectivity, performance, and resource usage.
	/// </summary>
	/// <param name="provider"> The persistence provider to check. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A detailed health check result. </returns>
	Task<DetailedHealthCheckResult> CheckDetailedHealthAsync(
		IPersistenceProvider provider,
		CancellationToken cancellationToken);
}
