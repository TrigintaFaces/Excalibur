// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain;
using Excalibur.Domain.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Excalibur.Hosting;

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

		// Defaults come from ApplicationContextDefaults, which the options path also uses. They were
		// once written out here and again, differently, over there -- and because this dictionary is
		// detached from IConfiguration, the options never saw what was added to it.
		_ = configContext.TryAdd(
			ApplicationContextDefaults.ApplicationNameKey,
			ApplicationContextDefaults.ApplicationName(builder.Environment));
		_ = configContext.TryAdd(
			ApplicationContextDefaults.ApplicationSystemNameKey,
			ApplicationContextDefaults.ApplicationSystemName(builder.Environment));

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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "ApplicationContextOptions is a simple POCO bound from configuration. All properties are preserved.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "ApplicationContextOptions is a simple POCO bound from configuration. All properties are preserved.")]
	public static IServiceCollection AddApplicationContext(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Bind, then complete, then validate -- in that order.
		//
		// The binding source is IConfiguration, which holds only what the consumer wrote. Anything
		// the framework considers a reasonable default has to be applied to the OPTIONS INSTANCE
		// after binding, not to some other copy of the configuration, or validation judges a value
		// the framework never gave it. PostConfigure is the hook that runs between the two, and it
		// takes IHostEnvironment because that is where a sensible application name comes from.
		//
		// A consumer value always wins: only blank fields are filled.
		_ = services.AddOptions<ApplicationContextOptions>()
			.Bind(configuration.GetSection(nameof(ApplicationContext)))
			.PostConfigure<IHostEnvironment>(ApplicationContextDefaults.Apply)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ApplicationContextOptions>>(new ApplicationContextOptionsValidator()));

		return services;
	}

}
