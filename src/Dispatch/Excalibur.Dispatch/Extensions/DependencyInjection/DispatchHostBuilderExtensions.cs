// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides common extension methods for IHostBuilder and IHostApplicationBuilder with consistent patterns.
/// </summary>
public static class DispatchHostBuilderExtensions
{
	/// <summary>
	/// Configures services on the host builder with fluent API support.
	/// </summary>
	/// <param name="builder"> The host builder. </param>
	/// <param name="configureServices"> The service configuration action. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder ConfigureServicesWithBuilder(
		this IHostBuilder builder,
		Action<IServiceCollection> configureServices)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureServices);

		return builder.ConfigureServices((_, services) => configureServices(services));
	}

	/// <summary>
	/// Configures services on the host application builder with fluent API support.
	/// </summary>
	/// <param name="builder"> The host application builder. </param>
	/// <param name="configureServices"> The service configuration action. </param>
	/// <returns> The host application builder for chaining. </returns>
	public static IHostApplicationBuilder ConfigureServicesWithBuilder(
		this IHostApplicationBuilder builder,
		Action<IServiceCollection> configureServices)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureServices);

		configureServices(builder.Services);
		return builder;
	}

	/// <summary>
	/// Conditionally configures services based on a predicate.
	/// </summary>
	/// <param name="builder"> The host builder. </param>
	/// <param name="condition"> The condition to evaluate. </param>
	/// <param name="configureServices"> The service configuration action. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder ConfigureServicesWhen(
		this IHostBuilder builder,
		bool condition,
		Action<IServiceCollection> configureServices)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureServices);

		if (condition)
		{
			_ = builder.ConfigureServices((_, services) => configureServices(services));
		}

		return builder;
	}

	/// <summary>
	/// Conditionally configures services based on a predicate.
	/// </summary>
	/// <param name="builder"> The host application builder. </param>
	/// <param name="condition"> The condition to evaluate. </param>
	/// <param name="configureServices"> The service configuration action. </param>
	/// <returns> The host application builder for chaining. </returns>
	public static IHostApplicationBuilder ConfigureServicesWhen(
		this IHostApplicationBuilder builder,
		bool condition,
		Action<IServiceCollection> configureServices)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureServices);

		if (condition)
		{
			configureServices(builder.Services);
		}

		return builder;
	}

	/// <summary>
	/// Configures services for a specific environment.
	/// </summary>
	/// <param name="builder"> The host builder. </param>
	/// <param name="environmentName"> The environment name to match. </param>
	/// <param name="configureServices"> The service configuration action. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder ConfigureServicesForEnvironment(
		this IHostBuilder builder,
		string environmentName,
		Action<IServiceCollection> configureServices)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureServices);

		return builder.ConfigureServices((context, services) =>
		{
			if (context.HostingEnvironment.IsEnvironment(environmentName))
			{
				configureServices(services);
			}
		});
	}

	/// <summary>
	/// Configures services only in development environment.
	/// </summary>
	/// <param name="builder"> The host builder. </param>
	/// <param name="configureServices"> The service configuration action. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder ConfigureServicesForDevelopment(
		this IHostBuilder builder,
		Action<IServiceCollection> configureServices)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureServices);

		return builder.ConfigureServices((context, services) =>
		{
			if (context.HostingEnvironment.IsDevelopment())
			{
				configureServices(services);
			}
		});
	}

	/// <summary>
	/// Configures services only in production environment.
	/// </summary>
	/// <param name="builder"> The host builder. </param>
	/// <param name="configureServices"> The service configuration action. </param>
	/// <returns> The host builder for chaining. </returns>
	public static IHostBuilder ConfigureServicesForProduction(
		this IHostBuilder builder,
		Action<IServiceCollection> configureServices)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureServices);

		return builder.ConfigureServices((context, services) =>
		{
			if (context.HostingEnvironment.IsProduction())
			{
				configureServices(services);
			}
		});
	}
}
