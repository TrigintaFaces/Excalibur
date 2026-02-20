// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides health and diagnostics capabilities for persistence providers.
/// Obtain via <see cref="IPersistenceProvider.GetService"/> with
/// <c>typeof(IPersistenceProviderHealth)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the ISP pattern — consumers that only need to execute
/// data requests use <see cref="IPersistenceProvider"/> directly.
/// Health checks and monitoring tools use this sub-interface.
/// </para>
/// <para>
/// Reference: <c>Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck</c> — single method;
/// this interface adds provider-specific diagnostics beyond simple pass/fail.
/// </para>
/// </remarks>
public interface IPersistenceProviderHealth
{
	/// <summary>
	/// Gets a value indicating whether the provider is currently available and healthy.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the provider is available and healthy; otherwise, <see langword="false"/>.
	/// </value>
	bool IsAvailable { get; }

	/// <summary>
	/// Tests the connection to the persistence provider.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> <see langword="true"/> if the connection is successful; otherwise, <see langword="false"/>. </returns>
	Task<bool> TestConnectionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets provider-specific health and performance metrics.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A dictionary of metric names and values. </returns>
	Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current connection pool statistics (if applicable).
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> Connection pool statistics or <see langword="null"/> if not applicable. </returns>
	Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken);
}
