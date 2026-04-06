// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring leader election services using the builder pattern.
/// </summary>
public static class LeaderElectionBuilderServiceCollectionExtensions
{
	/// <summary>
	/// Adds leader election services using the builder pattern for provider selection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure leader election via the builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcaliburLeaderElection(le => le
	///     .UseInMemory()
	///     .WithHealthChecks()
	///     .WithFencingTokens());
	/// </code>
	/// </example>
	public static IServiceCollection AddExcaliburLeaderElection(
		this IServiceCollection services,
		Action<ILeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register base options with validation
		_ = services.AddOptions<LeaderElectionOptions>()
			.ValidateOnStart();

		var builder = new LeaderElectionBuilder(services);
		configure(builder);

		return services;
	}
}
