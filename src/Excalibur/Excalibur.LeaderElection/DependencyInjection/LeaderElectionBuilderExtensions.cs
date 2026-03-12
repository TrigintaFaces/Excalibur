// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="ILeaderElectionBuilder"/> to add optional features.
/// </summary>
public static class LeaderElectionBuilderExtensions
{
	/// <summary>
	/// Adds health check integration for leader election.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ILeaderElectionBuilder WithHealthChecks(this ILeaderElectionBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.AddHealthChecks()
			.AddLeaderElectionHealthCheck();

		return builder;
	}

	/// <summary>
	/// Adds fencing token middleware support for leader election.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Registers <see cref="FencingTokenMiddleware"/>. The actual
	/// <see cref="IFencingTokenProvider"/> must be registered separately
	/// via <see cref="FencingTokenServiceCollectionExtensions.AddFencingTokenSupport{TProvider}"/>.
	/// </remarks>
	public static ILeaderElectionBuilder WithFencingTokens(this ILeaderElectionBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<FencingTokenMiddleware>();

		return builder;
	}

	/// <summary>
	/// Configures leader election options.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configure">Action to configure options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ILeaderElectionBuilder WithOptions(
		this ILeaderElectionBuilder builder,
		Action<LeaderElectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.Configure(configure);

		return builder;
	}
}
