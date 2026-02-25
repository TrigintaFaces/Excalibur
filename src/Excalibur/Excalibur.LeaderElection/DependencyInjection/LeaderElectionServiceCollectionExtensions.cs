// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring leader election services.
/// </summary>
public static class LeaderElectionServiceCollectionExtensions
{
	/// <summary>
	/// Adds leader election services to the service collection with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburLeaderElection(this IServiceCollection services)
	{
		return services.AddExcaliburLeaderElection(_ => { });
	}

	/// <summary>
	/// Adds leader election services to the service collection with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburLeaderElection(
		this IServiceCollection services,
		Action<LeaderElectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<LeaderElectionOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}
