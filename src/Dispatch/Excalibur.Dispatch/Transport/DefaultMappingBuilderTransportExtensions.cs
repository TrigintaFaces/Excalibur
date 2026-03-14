// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Transport-specific extension methods for <see cref="IDefaultMappingBuilder"/>.
/// </summary>
/// <remarks>
/// These extensions provide typed configuration for specific transport defaults (RabbitMQ, Kafka, etc.)
/// by delegating to the concrete <see cref="DefaultMappingBuilder"/>.
/// </remarks>
public static class DefaultMappingBuilderTransportExtensions
{
	/// <summary>
	/// Configures the default RabbitMQ mapping.
	/// </summary>
	/// <param name="builder">The default mapping builder.</param>
	/// <param name="configure">Action to configure the RabbitMQ context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IDefaultMappingBuilder ForRabbitMq(
		this IDefaultMappingBuilder builder,
		Action<IRabbitMqMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is DefaultMappingBuilder concrete)
		{
			concrete.Configuration.RabbitMqDefaults = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures the default Kafka mapping.
	/// </summary>
	/// <param name="builder">The default mapping builder.</param>
	/// <param name="configure">Action to configure the Kafka context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IDefaultMappingBuilder ForKafka(
		this IDefaultMappingBuilder builder,
		Action<IKafkaMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is DefaultMappingBuilder concrete)
		{
			concrete.Configuration.KafkaDefaults = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures the default Azure Service Bus mapping.
	/// </summary>
	/// <param name="builder">The default mapping builder.</param>
	/// <param name="configure">Action to configure the Azure Service Bus context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IDefaultMappingBuilder ForAzureServiceBus(
		this IDefaultMappingBuilder builder,
		Action<IAzureServiceBusMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is DefaultMappingBuilder concrete)
		{
			concrete.Configuration.AzureServiceBusDefaults = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures the default AWS SQS mapping.
	/// </summary>
	/// <param name="builder">The default mapping builder.</param>
	/// <param name="configure">Action to configure the AWS SQS context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IDefaultMappingBuilder ForAwsSqs(
		this IDefaultMappingBuilder builder,
		Action<IAwsSqsMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is DefaultMappingBuilder concrete)
		{
			concrete.Configuration.AwsSqsDefaults = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures the default Google Pub/Sub mapping.
	/// </summary>
	/// <param name="builder">The default mapping builder.</param>
	/// <param name="configure">Action to configure the Google Pub/Sub context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IDefaultMappingBuilder ForGooglePubSub(
		this IDefaultMappingBuilder builder,
		Action<IGooglePubSubMappingContext> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is DefaultMappingBuilder concrete)
		{
			concrete.Configuration.GooglePubSubDefaults = configure;
		}

		return builder;
	}
}
