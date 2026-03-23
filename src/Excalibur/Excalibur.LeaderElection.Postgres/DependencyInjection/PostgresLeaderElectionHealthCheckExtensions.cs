// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding Postgres leader election health checks.
/// </summary>
public static class PostgresLeaderElectionHealthCheckExtensions
{
	/// <summary>
	/// Default name for the Postgres leader election health check.
	/// </summary>
	private static readonly string DefaultName = "postgres-leader-election";

	/// <summary>
	/// Default tags for the Postgres leader election health check.
	/// </summary>
	private static readonly string[] DefaultTags = ["leader-election", "postgres"];

	/// <summary>
	/// Adds a Postgres leader election health check to the health checks builder.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The name of the health check. Default is "postgres-leader-election".</param>
	/// <param name="failureStatus">
	/// The health status to report when the check fails. If <see langword="null"/>, the default failure status is used.
	/// </param>
	/// <param name="tags">Optional tags to associate with the health check. Defaults to ["leader-election", "postgres"].</param>
	/// <returns>The health checks builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is a convenience extension for registering a <see cref="LeaderElectionHealthCheck"/>
	/// pre-configured with Postgres-specific defaults (name and tags).
	/// </para>
	/// <para>
	/// Requires <c>AddPostgresLeaderElection</c> to be called first so that
	/// <see cref="ILeaderElection"/> is available in the service container.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddPostgresLeaderElection(options => { ... });
	/// services.AddHealthChecks()
	///     .AddPostgresLeaderElectionHealthCheck();
	/// </code>
	/// </para>
	/// </remarks>
	public static IHealthChecksBuilder AddPostgresLeaderElectionHealthCheck(
		this IHealthChecksBuilder builder,
		string? name = null,
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.Add(new HealthCheckRegistration(
			name ?? DefaultName,
			sp => new LeaderElectionHealthCheck(
				sp.GetRequiredKeyedService<ILeaderElection>("default")),
			failureStatus,
			tags ?? DefaultTags));
	}
}
