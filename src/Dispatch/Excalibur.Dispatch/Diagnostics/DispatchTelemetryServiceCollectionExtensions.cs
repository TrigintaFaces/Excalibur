// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

// IDispatchTelemetryProvider moved to IDispatchTelemetryProvider.cs (MA0048 compliance).

/// <summary>
/// Service collection extensions for registering Dispatch telemetry and observability components. Implements R8.21 comprehensive telemetry
/// integration and R7.17 performance monitoring setup.
/// </summary>
/// <remarks>
/// Provides fluent API for configuring OpenTelemetry integration with Dispatch enhanced patterns. Supports both development and production
/// telemetry configurations with appropriate defaults.
/// </remarks>
public static class DispatchTelemetryServiceCollectionExtensions
{
	/// <summary>
	/// Adds Dispatch telemetry services to the service collection with default configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddDispatchTelemetry(this IServiceCollection services) =>
		services.AddDispatchTelemetry(static options => { _ = options; });

	/// <summary>
	/// Adds Dispatch telemetry services to the service collection with configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configureOptions"> Action to configure telemetry options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configureOptions is null. </exception>
	public static IServiceCollection AddDispatchTelemetry(
		this IServiceCollection services,
		Action<DispatchTelemetryOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.Configure(configureOptions);
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DispatchTelemetryOptions>, DispatchTelemetryOptionsValidator>());

		// Register core telemetry components
		services.TryAddSingleton<IDispatchTelemetryProvider, DispatchTelemetryProvider>();

		return services;
	}

	/// <summary>
	/// Adds Dispatch telemetry services using configuration section.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="configuration"> The configuration instance. </param>
	/// <param name="sectionName"> The configuration section name. Default is "DispatchTelemetry". </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services or configuration is null. </exception>
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "DispatchTelemetryOptions types are preserved through DI registration and have well-defined properties")]
	[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
		Justification = "DispatchTelemetryOptions has known serializable properties")]
	public static IServiceCollection AddDispatchTelemetry(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName = "DispatchTelemetry")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(sectionName);

		// Bind configuration
		_ = services.Configure<DispatchTelemetryOptions>(configuration.GetSection(sectionName));
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DispatchTelemetryOptions>, DispatchTelemetryOptionsValidator>());

		// Register core telemetry components
		services.TryAddSingleton<IDispatchTelemetryProvider, DispatchTelemetryProvider>();

		return services;
	}

	/// <summary>
	/// Adds Dispatch telemetry with production-optimized configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddDispatchTelemetryForProduction(this IServiceCollection services) =>
		services.AddDispatchTelemetry(options => DispatchTelemetryOptions.CreateProductionProfile().CopyTo(options));

	/// <summary>
	/// Adds Dispatch telemetry with development-optimized configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddDispatchTelemetryForDevelopment(this IServiceCollection services) =>
		services.AddDispatchTelemetry(options => DispatchTelemetryOptions.CreateDevelopmentProfile().CopyTo(options));

	/// <summary>
	/// Adds Dispatch telemetry with throughput-optimized configuration.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when services is null. </exception>
	public static IServiceCollection AddDispatchTelemetryForThroughput(this IServiceCollection services) =>
		services.AddDispatchTelemetry(options => DispatchTelemetryOptions.CreateThroughputProfile().CopyTo(options));
}

// DispatchTelemetryProvider moved to DispatchTelemetryProvider.cs (MA0048 compliance).
