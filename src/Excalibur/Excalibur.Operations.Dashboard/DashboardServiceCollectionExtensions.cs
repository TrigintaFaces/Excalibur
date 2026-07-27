// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Operations.Dashboard;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions for the Excalibur operational dashboard.
/// </summary>
public static class DashboardServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Excalibur operational dashboard: validated <see cref="DashboardOptions"/> and the
	/// read-endpoint module registry. Read endpoints are mapped by <c>MapDashboardApi</c>.
	/// </summary>
	/// <param name="services"> The service collection to add the dashboard to. </param>
	/// <returns> The same service collection, for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> is null. </exception>
	public static IServiceCollection AddDashboard(this IServiceCollection services) =>
		services.AddDashboard(static _ => { });

	/// <summary>
	/// Adds the Excalibur operational dashboard with the supplied options configuration.
	/// </summary>
	/// <param name="services"> The service collection to add the dashboard to. </param>
	/// <param name="configure"> A delegate to configure <see cref="DashboardOptions"/>. </param>
	/// <returns> The same service collection, for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> or <paramref name="configure"/> is null. </exception>
	public static IServiceCollection AddDashboard(this IServiceCollection services, Action<DashboardOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Validation is performed by DashboardOptionsValidator (below) rather than
		// ValidateDataAnnotations(), which is reflection-based and not trim/AOT-safe.
		services.AddOptions<DashboardOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DashboardOptions>, DashboardOptionsValidator>());

		// Built-in read-only subsystem modules. Each resolves its underlying store at request time via an
		// optional (nullable) service parameter, so an absent subsystem fails open rather than throwing.
		services.TryAddEnumerable(
		[
			ServiceDescriptor.Singleton<IDashboardEndpointModule, OutboxDashboardModule>(),
			ServiceDescriptor.Singleton<IDashboardEndpointModule, DeadLetterDashboardModule>(),
			ServiceDescriptor.Singleton<IDashboardEndpointModule, InboxDashboardModule>(),
			ServiceDescriptor.Singleton<IDashboardEndpointModule, LeaderDashboardModule>(),
			ServiceDescriptor.Singleton<IDashboardEndpointModule, SagaDashboardModule>(),
			ServiceDescriptor.Singleton<IDashboardEndpointModule, WorkflowDashboardModule>(),
		]);

		// Mutating modules (mapped only when EnableMutatingActions is true; see MapDashboardApi).
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDashboardMutatingModule, DeadLetterReplayMutatingModule>());

		return services;
	}
}
