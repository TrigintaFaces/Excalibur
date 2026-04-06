// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Hosting.Builders;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Excalibur hosting builder extensions for leader election configuration.
/// </summary>
public static class LeaderElectionExcaliburBuilderExtensions
{
	/// <summary>
	/// Configures leader election for the Excalibur host.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">
	/// Optional action to configure leader election options. Pass <see langword="null"/> to use defaults.
	/// </param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IExcaliburBuilder AddLeaderElection(
		this IExcaliburBuilder builder,
		Action<LeaderElectionOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddExcaliburLeaderElection(configure);
		}
		else
		{
			_ = builder.Services.AddExcaliburLeaderElection();
		}

		return builder;
	}

	/// <summary>
	/// Configures leader election for the Excalibur host using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="LeaderElectionOptions"/>.</param>
	/// <returns>The same builder for fluent chaining.</returns>
	public static IExcaliburBuilder AddLeaderElection(
		this IExcaliburBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<LeaderElectionOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		_ = builder.Services.AddExcaliburLeaderElection();

		return builder;
	}
}
