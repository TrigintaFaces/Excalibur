// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Observability.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AWS observability services.
/// </summary>
public static class AwsObservabilityServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS X-Ray and CloudWatch observability integration services.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	public static IServiceCollection AddAwsObservability(
		this IServiceCollection services,
		Action<AwsObservabilityOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<AwsObservabilityOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AwsObservabilityOptions>, AwsObservabilityOptionsValidator>());

		services.TryAddSingleton<IAwsTracingIntegration, AwsTracingIntegration>();

		// Activate the X-Ray / CloudWatch listeners at startup (each self-gates on its enable flag).
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AwsTracingActivationHostedService>());

		return services;
	}

	/// <summary>
	/// Adds AWS X-Ray and CloudWatch observability integration services
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind AWS observability options from.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
	public static IServiceCollection AddAwsObservability(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AwsObservabilityOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AwsObservabilityOptions>, AwsObservabilityOptionsValidator>());

		services.TryAddSingleton<IAwsTracingIntegration, AwsTracingIntegration>();

		// Activate the X-Ray / CloudWatch listeners at startup (each self-gates on its enable flag).
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AwsTracingActivationHostedService>());

		return services;
	}
}
