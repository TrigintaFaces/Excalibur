// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding SQL Server leader election health checks.
/// </summary>
public static class SqlServerLeaderElectionHealthCheckExtensions
{
	/// <summary>
	/// Default name for the SQL Server leader election health check.
	/// </summary>
	private static readonly string DefaultName = "sql-server-leader-election";

	/// <summary>
	/// Default tags for the SQL Server leader election health check.
	/// </summary>
	private static readonly string[] DefaultTags = ["leader-election", "sql-server"];

	/// <summary>
	/// Adds a SQL Server leader election health check to the health checks builder.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The name of the health check. Default is "sql-server-leader-election".</param>
	/// <param name="failureStatus">
	/// The health status to report when the check fails. If <see langword="null"/>, the default failure status is used.
	/// </param>
	/// <param name="tags">Optional tags to associate with the health check. Defaults to ["leader-election", "sql-server"].</param>
	/// <returns>The health checks builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is a convenience extension for registering a <see cref="LeaderElectionHealthCheck"/>
	/// pre-configured with SQL Server-specific defaults (name and tags).
	/// </para>
	/// <para>
	/// Requires <c>AddSqlServerLeaderElection</c> to be called first so that
	/// <see cref="ILeaderElection"/> is available in the service container.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddSqlServerLeaderElection(connectionString, "MyApp.Leader");
	/// services.AddHealthChecks()
	///     .AddSqlServerLeaderElectionHealthCheck();
	/// </code>
	/// </para>
	/// </remarks>
	public static IHealthChecksBuilder AddSqlServerLeaderElectionHealthCheck(
		this IHealthChecksBuilder builder,
		string? name = null,
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name ?? DefaultName,
			sp => new LeaderElectionHealthCheck(
				sp.GetRequiredService<ILeaderElection>()),
			failureStatus,
			tags ?? DefaultTags));
	}
}
