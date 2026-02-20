// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Watch;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring leader election watch services.
/// </summary>
public static class LeaderElectionWatchServiceCollectionExtensions
{
	/// <summary>
	/// Adds leader election watcher services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure leader watch options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddLeaderElectionWatcher(
		this IServiceCollection services,
		Action<LeaderWatchOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<LeaderWatchOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<ILeaderElectionWatcher, DefaultLeaderElectionWatcher>();

		return services;
	}

	/// <summary>
	/// Adds leader election watcher services with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddLeaderElectionWatcher(this IServiceCollection services)
	{
		return services.AddLeaderElectionWatcher(_ => { });
	}
}
