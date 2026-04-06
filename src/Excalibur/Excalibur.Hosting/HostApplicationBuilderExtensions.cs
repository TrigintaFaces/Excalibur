// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Domain;
using Excalibur.Domain.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for configuring application-specific services and features in an <see cref="IHostApplicationBuilder" />.
/// </summary>
public static class HostApplicationBuilderExtensions
{
	/// <summary>
	/// Configures the <see cref="ApplicationContext" /> with settings from the application configuration and environment.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureApplicationContext(this IHostApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var configContext = builder.Configuration.GetApplicationContextConfiguration();

		// Add default values if not present in configuration
		_ = configContext.TryAdd("ApplicationName", builder.Environment.ApplicationName);
		_ = configContext.TryAdd("ApplicationSystemName", builder.Environment.ApplicationName.ToKebabCaseLower(clean: true));

		ApplicationContext.Init(configContext);

		// Also register IOptions<ApplicationContextOptions> for DI-based access
		builder.Services.AddApplicationContext(builder.Configuration);

		return builder;
	}

	/// <summary>
	/// Registers <see cref="ApplicationContextOptions"/> with the DI container, bound from the
	/// <c>ApplicationContext</c> configuration section. Consumers can inject
	/// <see cref="Options.IOptions{ApplicationContextOptions}"/> instead
	/// of using the static <see cref="ApplicationContext"/> API.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <param name="configuration">The application configuration.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> for chaining.</returns>
	public static IServiceCollection AddApplicationContext(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ApplicationContextOptions>()
			.Bind(configuration.GetSection(nameof(ApplicationContext)))
			.ValidateOnStart();

		return services;
	}

}
