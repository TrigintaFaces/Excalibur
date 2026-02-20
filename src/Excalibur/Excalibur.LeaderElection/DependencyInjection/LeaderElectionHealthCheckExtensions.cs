// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding leader election health checks.
/// </summary>
public static class LeaderElectionHealthCheckExtensions
{
	/// <summary>
	/// Adds a leader election health check to the health checks builder.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The name of the health check. Default is "leader-election".</param>
	/// <param name="failureStatus">
	/// The health status to report when the check fails. If <see langword="null"/>, the default failure status is used.
	/// </param>
	/// <param name="tags">Optional tags to associate with the health check.</param>
	/// <returns>The health checks builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This health check requires an <see cref="ILeaderElection"/> to be registered in DI.
	/// Use <c>AddSqlServerLeaderElection</c>, <c>AddRedisLeaderElection</c>, or register a custom implementation.
	/// </para>
	/// <para>
	/// The health check is provider-agnostic — it works with any <see cref="ILeaderElection"/> implementation.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddSqlServerLeaderElection(connectionString, "MyApp.Leader");
	/// services.AddHealthChecks()
	///     .AddLeaderElectionHealthCheck();
	/// </code>
	/// </para>
	/// </remarks>
	public static IHealthChecksBuilder AddLeaderElectionHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "leader-election",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => new LeaderElectionHealthCheck(
				sp.GetRequiredService<ILeaderElection>()),
			failureStatus,
			tags));
	}
}
