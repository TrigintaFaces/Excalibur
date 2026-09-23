// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Consul;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Consul-backed fencing token provider.
/// </summary>
public static class ConsulFencingTokenServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Consul-backed <see cref="IFencingTokenProvider"/> provider and the fencing token
	/// middleware.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires a <see cref="global::Consul.IConsulClient"/> to be registered (the same client used by
	/// <c>UseConsul(...)</c> leader election). Uses <c>TryAdd</c> so a consumer-supplied provider takes
	/// precedence. <c>UseConsul(...)</c> / <c>AddConsulLeaderElection(...)</c> already register this by
	/// default (fencing is on by default; <c>WithoutFencingTokens()</c> opts out) — this method exists for
	/// a consumer composing services manually without going through those entry points, and satisfies the
	/// startup prerequisite check the same way.
	/// </remarks>
	public static IServiceCollection AddConsulFencingTokenProvider(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IFencingTokenProvider, ConsulFencingTokenProvider>();
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}
}
