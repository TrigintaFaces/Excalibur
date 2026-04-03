// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS CloudWatch metrics export bridge with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Registers the <see cref="CloudWatchMetricsOptions"/> and optionally a concrete
/// <see cref="ICloudWatchMetricsExporter"/> implementation for bridging
/// OpenTelemetry metrics to AWS CloudWatch.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsCloudWatchMetricsExporter(options =>
/// {
///     options.Namespace = "MyApp/Dispatch";
///     options.Region = "us-east-1";
///     options.PublishInterval = TimeSpan.FromSeconds(60);
/// });
/// </code>
/// </example>
public static class CloudWatchMetricsServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS CloudWatch metrics export support with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure CloudWatch metrics options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddAwsCloudWatchMetricsExporter(
		this IServiceCollection services,
		Action<CloudWatchMetricsOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CloudWatchMetricsOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds AWS CloudWatch metrics export support using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="CloudWatchMetricsOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	public static IServiceCollection AddAwsCloudWatchMetricsExporter(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CloudWatchMetricsOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds a concrete AWS CloudWatch metrics exporter with the specified configuration.
	/// </summary>
	/// <typeparam name="TImplementation">
	/// The concrete type implementing <see cref="ICloudWatchMetricsExporter"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure CloudWatch metrics options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddAwsCloudWatchMetricsExporter<TImplementation>(
		this IServiceCollection services,
		Action<CloudWatchMetricsOptions> configure)
		where TImplementation : class, ICloudWatchMetricsExporter
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CloudWatchMetricsOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton<ICloudWatchMetricsExporter, TImplementation>();

		return services;
	}

	/// <summary>
	/// Adds a concrete AWS CloudWatch metrics exporter using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <typeparam name="TImplementation">
	/// The concrete type implementing <see cref="ICloudWatchMetricsExporter"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="CloudWatchMetricsOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	public static IServiceCollection AddAwsCloudWatchMetricsExporter<TImplementation>(
		this IServiceCollection services,
		IConfiguration configuration)
		where TImplementation : class, ICloudWatchMetricsExporter
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CloudWatchMetricsOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton<ICloudWatchMetricsExporter, TImplementation>();

		return services;
	}
}
