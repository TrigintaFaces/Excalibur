// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Kafka admin client support with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Registers the <see cref="KafkaAdminOptions"/> and optionally a concrete
/// <see cref="IKafkaAdminClient"/> implementation for topic management operations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddKafkaAdmin(options =>
/// {
///     options.BootstrapServers = "broker1:9092,broker2:9092";
///     options.OperationTimeout = TimeSpan.FromSeconds(30);
/// });
/// </code>
/// </example>
public static class KafkaAdminServiceCollectionExtensions
{
	/// <summary>
	/// Adds Kafka admin client support with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure admin client options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddKafkaAdmin(
		this IServiceCollection services,
		Action<KafkaAdminOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<KafkaAdminOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Adds a concrete Kafka admin client with the specified configuration.
	/// </summary>
	/// <typeparam name="TImplementation">
	/// The concrete type implementing <see cref="IKafkaAdminClient"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure admin client options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddKafkaAdmin<TImplementation>(
		this IServiceCollection services,
		Action<KafkaAdminOptions> configure)
		where TImplementation : class, IKafkaAdminClient
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<KafkaAdminOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton<IKafkaAdminClient, TImplementation>();

		return services;
	}
}
