// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Postgres;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Postgres-backed fencing token provider.
/// </summary>
public static class PostgresFencingTokenServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Postgres-backed <see cref="IFencingTokenProvider"/> (a dedicated per-resource
	/// <c>SEQUENCE</c> as the monotonic mint) and the fencing token middleware.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">
	/// The Postgres connection string — the same database used by <c>UsePostgres(...)</c> leader election. The
	/// provider opens its own short-lived pooled connections (independent of the election's dedicated lock
	/// connection).
	/// </param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Uses <c>TryAdd</c> so a consumer-supplied provider takes precedence. Pair with <c>WithFencingTokens()</c>
	/// on the leader election builder; the startup prerequisite check then passes because a provider is
	/// registered, and each leadership acquisition advances the fence before declaring leadership (fail-closed).
	/// </remarks>
	public static IServiceCollection AddPostgresFencingTokenProvider(this IServiceCollection services, string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		services.TryAddSingleton<IFencingTokenProvider>(_ => new PostgresFencingTokenProvider(connectionString));
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}
}
