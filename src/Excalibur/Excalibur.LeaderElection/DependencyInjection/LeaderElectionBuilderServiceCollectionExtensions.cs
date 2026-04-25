// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Internal extension methods for configuring leader election services using the builder pattern.
/// Consumers opt-in via <c>IExcaliburBuilder.AddLeaderElection(...)</c>.
/// </summary>
internal static class LeaderElectionBuilderServiceCollectionExtensions
{
	/// <summary>
	/// Adds leader election services using the builder pattern for provider selection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure leader election via the builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddLeaderElection(le => le
	///     .UseInMemory()
	///     .WithHealthChecks()
	///     .WithFencingTokens()));
	/// </code>
	/// </example>
	internal static IServiceCollection AddExcaliburLeaderElection(
		this IServiceCollection services,
		Action<ILeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register base options with validation
		_ = services.AddOptions<LeaderElectionOptions>()
			.ValidateOnStart();

		// bd-x6rg45: fail loud at host start if the consumer forgot to pick a provider.
		// Idempotent via TryAddEnumerable — re-registering AddLeaderElection does not
		// double-register the validator.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, LeaderElectionPrerequisiteValidator>());

		// Non-keyed convenience aliases: forward to keyed "default" so consumers
		// can inject ILeaderElection / ILeaderElectionFactory directly without [FromKeyedServices("default")].
		services.TryAddSingleton<ILeaderElection>(sp =>
			sp.GetRequiredKeyedService<ILeaderElection>("default"));
		services.TryAddSingleton<ILeaderElectionFactory>(sp =>
			sp.GetRequiredKeyedService<ILeaderElectionFactory>("default"));

		var builder = new LeaderElectionBuilder(services);
		configure(builder);

		return services;
	}
}
