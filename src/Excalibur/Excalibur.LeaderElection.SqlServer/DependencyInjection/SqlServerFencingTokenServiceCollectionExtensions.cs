// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the SQL Server-backed fencing token provider.
/// </summary>
public static class SqlServerFencingTokenServiceCollectionExtensions
{
	/// <summary>
	/// Registers the SQL Server-backed <see cref="IFencingTokenProvider"/> (a dedicated per-resource
	/// <c>SEQUENCE</c> as the monotonic mint) and the fencing token middleware.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">
	/// The SQL Server connection string — the same database used by <c>UseSqlServer(...)</c> leader election.
	/// The provider opens its own short-lived pooled connections (independent of the election's dedicated lock
	/// connection).
	/// </param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Uses <c>TryAdd</c> so a consumer-supplied provider takes precedence. <c>UseSqlServer(...)</c> /
	/// <c>AddSqlServerHealthBasedLeaderElection(...)</c> already register this by default (fencing is on by
	/// default; <c>WithoutFencingTokens()</c> opts out) — this method exists for a consumer composing
	/// services manually without going through those entry points, and satisfies the startup prerequisite
	/// check the same way. Each leadership acquisition advances the fence before declaring leadership
	/// (fail-closed).
	/// </remarks>
	public static IServiceCollection AddSqlServerFencingTokenProvider(this IServiceCollection services, string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		services.TryAddSingleton<IFencingTokenProvider>(_ => new SqlServerFencingTokenProvider(connectionString));
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}
}
