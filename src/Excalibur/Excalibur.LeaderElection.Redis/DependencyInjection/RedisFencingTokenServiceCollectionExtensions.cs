// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Redis;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Redis-backed fencing token provider.
/// </summary>
public static class RedisFencingTokenServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Redis-backed <see cref="IFencingTokenProvider"/> reference provider and the
	/// fencing token middleware.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="StackExchange.Redis.IConnectionMultiplexer"/> to be registered (the same
	/// connection used by <c>UseRedis(...)</c> leader election). Uses <c>TryAdd</c> so a consumer-supplied
	/// provider takes precedence. <c>UseRedis(...)</c> already registers this by default (fencing is on by
	/// default; <c>WithoutFencingTokens()</c> opts out) — this method exists for a consumer composing
	/// services manually without going through that entry point, and satisfies the startup prerequisite
	/// check the same way.
	/// </remarks>
	public static IServiceCollection AddRedisFencingTokenProvider(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IFencingTokenProvider, RedisFencingTokenProvider>();
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}
}
