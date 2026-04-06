// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
		return services.AddExcaliburLeaderElection(static (LeaderElectionOptions _) => { });
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
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds leader election services to the service collection
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddExcaliburLeaderElection(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<LeaderElectionOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds leader election services with a pre-built options instance.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The pre-built leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This overload registers the options via <c>IOptions&lt;T&gt;</c> using the provided
	/// instance directly, eliminating the need for an <see cref="Action{T}"/> delegate.
	/// Useful when options are constructed from external configuration or shared across services.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var options = new LeaderElectionOptions
	/// {
	///     LeaseDuration = TimeSpan.FromSeconds(30),
	///     RenewInterval = TimeSpan.FromSeconds(10)
	/// };
	/// services.AddExcaliburLeaderElection(options);
	/// </code>
	/// </example>
	public static IServiceCollection AddExcaliburLeaderElection(
		this IServiceCollection services,
		LeaderElectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		services.TryAddSingleton(Microsoft.Extensions.Options.Options.Create(options));

		return services;
	}
}
