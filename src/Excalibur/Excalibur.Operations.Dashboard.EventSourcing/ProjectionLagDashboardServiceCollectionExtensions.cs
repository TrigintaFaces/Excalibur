// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Operations.Dashboard;
using Excalibur.Operations.Dashboard.EventSourcing;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extension for the dashboard projection/CDC-lag panel. Kept in a dedicated package so the
/// base dashboard has no event-sourcing dependency.
/// </summary>
public static class ProjectionLagDashboardServiceCollectionExtensions
{
	/// <summary>
	/// Adds the projection-lag read module to the dashboard. Call after <c>AddDashboard()</c>; the module
	/// is auto-discovered and mapped by <c>MapDashboardApi()</c> and appears in capability discovery.
	/// </summary>
	/// <remarks>
	/// The module resolves <c>IProjectionLagReadModel</c> at request time as an optional service, so a host
	/// without event sourcing configured fails open (reports not-configured) rather than throwing.
	/// </remarks>
	/// <param name="services">The service collection to add the projection-lag panel to.</param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddProjectionLagDashboard(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton(TimeProvider.System);
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDashboardEndpointModule, ProjectionLagDashboardModule>());

		return services;
	}
}
