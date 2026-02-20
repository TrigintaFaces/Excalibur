// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering RabbitMQ stream queue support with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// RabbitMQ streams are append-only log data structures that provide high throughput,
/// non-destructive consumption, and time-based or offset-based message replay.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddRabbitMqStreamQueues(options =>
/// {
///     options.StreamName = "my-events";
///     options.MaxAge = TimeSpan.FromDays(7);
///     options.MaxLength = 1_000_000_000;
///     options.SegmentSize = 500_000_000;
/// });
/// </code>
/// </example>
public static class RabbitMqStreamServiceCollectionExtensions
{
	/// <summary>
	/// Adds RabbitMQ stream queue support with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure stream queue options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers <see cref="RabbitMqStreamOptions"/> in the DI container with data annotation
	/// validation and startup validation. The consumer implementation should be registered
	/// separately as <see cref="IRabbitMqStreamConsumer"/>.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddRabbitMqStreamQueues(
		this IServiceCollection services,
		Action<RabbitMqStreamOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RabbitMqStreamOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}
}
