// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering RabbitMQ Management API client with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// The RabbitMQ Management plugin exposes an HTTP API for monitoring and managing
/// the broker. These extensions register the <see cref="RabbitMqManagementOptions"/>
/// and optionally the <see cref="IRabbitMqManagementClient"/> implementation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMqManagement(options =>
/// {
///     options.BaseUrl = "http://rabbitmq:15672";
///     options.Username = "admin";
///     options.Password = "secret";
/// });
/// </code>
/// </example>
public static class ManagementServiceCollectionExtensions
{
	/// <summary>
	/// Adds RabbitMQ Management API client support with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure management options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="RabbitMqManagementOptions"/> in the DI container with data annotation
	/// validation and startup validation. The management client implementation should be
	/// registered separately as <see cref="IRabbitMqManagementClient"/>.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddRabbitMqManagement(
		this IServiceCollection services,
		Action<RabbitMqManagementOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RabbitMqManagementOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds a concrete RabbitMQ Management API client with the specified configuration.
	/// </summary>
	/// <typeparam name="TImplementation">
	/// The concrete type implementing <see cref="IRabbitMqManagementClient"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure management options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddRabbitMqManagement<TImplementation>(
		this IServiceCollection services,
		Action<RabbitMqManagementOptions> configure)
		where TImplementation : class, IRabbitMqManagementClient
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RabbitMqManagementOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton<IRabbitMqManagementClient, TImplementation>();

		return services;
	}
}
