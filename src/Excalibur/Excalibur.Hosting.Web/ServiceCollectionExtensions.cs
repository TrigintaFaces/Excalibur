// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;

using Asp.Versioning;

using Excalibur.Hosting.Web.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Excalibur web services in the application's dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds global exception handling services with customizable problem details configuration.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="configureOptions"> Optional configuration action for customizing problem details options. </param>
	/// <returns> The configured service collection for method chaining. </returns>
	public static IServiceCollection AddGlobalExceptionHandler(
		this IServiceCollection services,
		Action<ProblemDetailsOptions>? configureOptions = null)
	{
		_ = services.Configure(configureOptions ?? (static _ => { }));
		_ = services.AddProblemDetails();
		_ = services.AddExceptionHandler<GlobalExceptionHandler>();
		return services;
	}

	/// <summary>
	/// Adds the necessary services for Excalibur web applications, including problem details, exception handling, API versioning, and core services.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configuration"> The application's configuration settings. </param>
	/// <param name="assemblies"> The assemblies to scan for application and data services. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> or <paramref name="configuration" /> is null. </exception>
	public static IServiceCollection AddExcaliburWebServices(this IServiceCollection services, IConfiguration configuration,
		params Assembly[] assemblies)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddExcaliburBaseServices(assemblies);

		// Configure API versioning
		services.TryAddSingleton(static _ => new ApiVersioningOptions
		{
			AssumeDefaultVersionWhenUnspecified = true,
			DefaultApiVersion = new ApiVersion(1, 0),
			ReportApiVersions = true,
			ApiVersionReader = ApiVersionReader.Combine(
				new UrlSegmentApiVersionReader(),
				new QueryStringApiVersionReader("api-version"),
				new HeaderApiVersionReader("X-Api-Version")),
		});

		_ = services
			.AddApiVersioning()
			.AddApiExplorer(static options =>
			{
				// ReSharper disable once StringLiteralTypo
				options.GroupNameFormat = "'v'VVV";
				options.SubstituteApiVersionInUrl = true;
			});

		return services;
	}
}
