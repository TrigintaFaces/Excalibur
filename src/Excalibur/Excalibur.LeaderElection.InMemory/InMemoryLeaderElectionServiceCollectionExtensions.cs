// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering in-memory leader election services.
/// </summary>
public static class InMemoryLeaderElectionServiceCollectionExtensions
{
	/// <summary>
	/// Adds in-memory leader election services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This is suitable for single-process scenarios, testing, and development.
	/// For distributed scenarios, use a provider like SqlServer, Redis, Consul, or Kubernetes.
	/// </remarks>
	public static IServiceCollection AddInMemoryLeaderElection(this IServiceCollection services)
	{
		return services.AddInMemoryLeaderElection(_ => { });
	}

	/// <summary>
	/// Adds in-memory leader election services to the service collection with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This is suitable for single-process scenarios, testing, and development.
	/// For distributed scenarios, use a provider like SqlServer, Redis, Consul, or Kubernetes.
	/// </remarks>
	public static IServiceCollection AddInMemoryLeaderElection(
		this IServiceCollection services,
		Action<LeaderElectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<ILeaderElectionFactory, InMemoryLeaderElectionFactory>();

		return services;
	}
}
