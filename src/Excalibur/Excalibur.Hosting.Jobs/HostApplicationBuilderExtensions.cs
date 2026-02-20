// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using Quartz;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for configuring Excalibur job hosting services in an <see cref="IHostApplicationBuilder" />.
/// </summary>
public static class ExcaliburJobHostBuilderExtensions
{
	/// <summary>
	/// Adds Excalibur Job Host services to the specified host application builder.
	/// </summary>
	/// <param name="builder"> The host application builder to configure. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IHostApplicationBuilder" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	/// <remarks>
	/// This method is ideal for .NET Worker Service applications and provides a fluent way to configure job hosting services directly on
	/// the host builder.
	/// </remarks>
	public static IHostApplicationBuilder AddExcaliburJobHost(
		this IHostApplicationBuilder builder,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Use explicit class reference to avoid ambiguity with HostingJobsServiceCollectionExtensions
		_ = JobHostServiceCollectionExtensions.AddExcaliburJobHost(builder.Services, assemblies);

		return builder;
	}

	/// <summary>
	/// Adds Excalibur Job Host services with custom Quartz configuration to the specified host application builder.
	/// </summary>
	/// <param name="builder"> The host application builder to configure. </param>
	/// <param name="configureQuartz"> Optional action to configure Quartz services. </param>
	/// <param name="assemblies"> An array of assemblies to scan for services and jobs. </param>
	/// <returns> The configured <see cref="IHostApplicationBuilder" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	/// <remarks>
	/// This overload allows for custom Quartz.NET configuration and is perfect for production scenarios requiring persistent job stores,
	/// clustering, or advanced scheduling features.
	/// </remarks>
	public static IHostApplicationBuilder AddExcaliburJobHost(
		this IHostApplicationBuilder builder,
		Action<IServiceCollectionQuartzConfigurator>? configureQuartz,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Use explicit class reference to avoid ambiguity with HostingJobsServiceCollectionExtensions
		_ = JobHostServiceCollectionExtensions.AddExcaliburJobHost(builder.Services, configureQuartz, assemblies);

		return builder;
	}
}
