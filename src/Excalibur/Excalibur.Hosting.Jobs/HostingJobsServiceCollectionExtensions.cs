// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;

using Quartz;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Excalibur job hosting services in an <see cref="IServiceCollection" />.
/// </summary>
public static class HostingJobsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur Job Host services to the specified service collection.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	/// <remarks>
	/// This method sets up a complete job hosting environment including:
	/// <list type="bullet">
	/// <item>
	/// <description> Excalibur base services (data, application, domain layers) </description>
	/// </item>
	/// <item>
	/// <description> Context services (TenantId, CorrelationId, ETag, ClientAddress) </description>
	/// </item>
	/// <item>
	/// <description> Quartz.NET job scheduling with dependency injection </description>
	/// </item>
	/// <item>
	/// <description> Health checks for job monitoring </description>
	/// </item>
	/// </list>
	/// <para> This is the recommended method for .NET Worker Service applications that need job scheduling capabilities. </para>
	/// </remarks>
	public static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Delegate to the Quartz-specific implementation
		return JobHostServiceCollectionExtensions.AddExcaliburJobHost(services, assemblies);
	}

	/// <summary>
	/// Adds Excalibur Job Host services with custom Quartz configuration.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configureQuartz"> Optional action to configure Quartz services. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	/// <remarks>
	/// This overload allows for custom Quartz.NET configuration such as persistent job stores, clustering, or custom schedulers. Perfect
	/// for production environments requiring advanced scheduling features.
	/// </remarks>
	public static IServiceCollection AddExcaliburJobHost(this IServiceCollection services,
		Action<IServiceCollectionQuartzConfigurator>? configureQuartz,
		params Assembly[] assemblies) =>

		// Delegate to the Quartz-specific implementation
		JobHostServiceCollectionExtensions.AddExcaliburJobHost(services, configureQuartz, assemblies);
}
