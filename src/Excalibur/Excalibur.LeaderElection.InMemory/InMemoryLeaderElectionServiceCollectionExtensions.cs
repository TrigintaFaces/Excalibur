// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.InMemory;

using Microsoft.Extensions.Configuration;
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
		services.AddKeyedSingleton<ILeaderElectionFactory>("inmemory",
			(sp, _) => sp.GetRequiredService<InMemoryLeaderElectionFactory>());
		services.TryAddKeyedSingleton<ILeaderElectionFactory>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElectionFactory>("inmemory"));
		services.TryAddSingleton<InMemoryLeaderElectionFactory>();

		return services;
	}

	/// <summary>
	/// Adds in-memory leader election services to the service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This is suitable for single-process scenarios, testing, and development.
	/// For distributed scenarios, use a provider like SqlServer, Redis, Consul, or Kubernetes.
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddInMemoryLeaderElection(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<LeaderElectionOptions>()
			.Bind(configuration);
		services.AddKeyedSingleton<ILeaderElectionFactory>("inmemory",
			(sp, _) => sp.GetRequiredService<InMemoryLeaderElectionFactory>());
		services.TryAddKeyedSingleton<ILeaderElectionFactory>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElectionFactory>("inmemory"));
		services.TryAddSingleton<InMemoryLeaderElectionFactory>();

		return services;
	}
}
