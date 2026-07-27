// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Operations.Dashboard;
using Excalibur.Operations.Dashboard.Throughput;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extension for the dashboard throughput (rate) panel, which bridges the existing
/// Excalibur OpenTelemetry counter meters into per-subsystem throughput rates.
/// </summary>
public static class ThroughputDashboardServiceCollectionExtensions
{
	/// <summary>
	/// Adds the throughput read module and its meter-bridging collector to the dashboard. Call after
	/// <c>AddDashboard()</c>; the module is auto-discovered and mapped by <c>MapDashboardApi()</c>.
	/// </summary>
	/// <param name="services">The service collection to add the throughput panel to.</param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddThroughputDashboard(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// The collector starts a MeterListener in its constructor; a single shared instance accumulates
		// counter measurements for the lifetime of the host.
		services.TryAddSingleton<ThroughputCollector>();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IDashboardEndpointModule, ThroughputDashboardModule>());

		return services;
	}
}
