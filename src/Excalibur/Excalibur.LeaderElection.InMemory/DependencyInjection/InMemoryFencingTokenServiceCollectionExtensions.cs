// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the in-memory-backed fencing token provider.
/// </summary>
public static class InMemoryFencingTokenServiceCollectionExtensions
{
	/// <summary>
	/// Registers the in-memory-backed <see cref="IFencingTokenProvider"/> provider and the fencing token
	/// middleware.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <c>AddInMemoryLeaderElection()</c> / <c>UseInMemory()</c> already register this by default (fencing
	/// is on by default; <c>WithoutFencingTokens()</c> opts out) — this method exists for a consumer
	/// composing services manually without going through those entry points. Uses <c>TryAdd</c> so a
	/// consumer-supplied provider takes precedence.
	/// </remarks>
	public static IServiceCollection AddInMemoryFencingTokenProvider(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IFencingTokenProvider, InMemoryFencingTokenProvider>();
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}
}
