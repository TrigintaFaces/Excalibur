// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Transport-specific extension methods for <see cref="IMessageTypeMappingBuilder{TMessage}"/>.
/// </summary>
/// <remarks>
/// These extensions provide typed configuration for specific transports (RabbitMQ, Kafka, etc.)
/// by delegating to the concrete <see cref="MessageTypeMappingBuilder{TMessage}"/>.
/// </remarks>
public static class MessageTypeMappingBuilderTransportExtensions
{
	/// <summary>
	/// Configures RabbitMQ-specific mapping for this message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type being configured.</typeparam>
	/// <param name="builder">The message type mapping builder.</param>
	/// <param name="configure">Action to configure the RabbitMQ context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageTypeMappingBuilder<TMessage> ToRabbitMq<TMessage>(
		this IMessageTypeMappingBuilder<TMessage> builder,
		Action<IRabbitMqMappingContext> configure)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is MessageTypeMappingBuilder<TMessage> concrete)
		{
			concrete.Configuration.RabbitMqConfiguration = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures Kafka-specific mapping for this message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type being configured.</typeparam>
	/// <param name="builder">The message type mapping builder.</param>
	/// <param name="configure">Action to configure the Kafka context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageTypeMappingBuilder<TMessage> ToKafka<TMessage>(
		this IMessageTypeMappingBuilder<TMessage> builder,
		Action<IKafkaMappingContext> configure)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is MessageTypeMappingBuilder<TMessage> concrete)
		{
			concrete.Configuration.KafkaConfiguration = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures Azure Service Bus-specific mapping for this message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type being configured.</typeparam>
	/// <param name="builder">The message type mapping builder.</param>
	/// <param name="configure">Action to configure the Azure Service Bus context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageTypeMappingBuilder<TMessage> ToAzureServiceBus<TMessage>(
		this IMessageTypeMappingBuilder<TMessage> builder,
		Action<IAzureServiceBusMappingContext> configure)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is MessageTypeMappingBuilder<TMessage> concrete)
		{
			concrete.Configuration.AzureServiceBusConfiguration = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures AWS SQS-specific mapping for this message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type being configured.</typeparam>
	/// <param name="builder">The message type mapping builder.</param>
	/// <param name="configure">Action to configure the AWS SQS context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageTypeMappingBuilder<TMessage> ToAwsSqs<TMessage>(
		this IMessageTypeMappingBuilder<TMessage> builder,
		Action<IAwsSqsMappingContext> configure)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is MessageTypeMappingBuilder<TMessage> concrete)
		{
			concrete.Configuration.AwsSqsConfiguration = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures AWS SNS-specific mapping for this message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type being configured.</typeparam>
	/// <param name="builder">The message type mapping builder.</param>
	/// <param name="configure">Action to configure the AWS SNS context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageTypeMappingBuilder<TMessage> ToAwsSns<TMessage>(
		this IMessageTypeMappingBuilder<TMessage> builder,
		Action<IAwsSnsMappingContext> configure)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is MessageTypeMappingBuilder<TMessage> concrete)
		{
			concrete.Configuration.AwsSnsConfiguration = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures Google Pub/Sub-specific mapping for this message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type being configured.</typeparam>
	/// <param name="builder">The message type mapping builder.</param>
	/// <param name="configure">Action to configure the Google Pub/Sub context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageTypeMappingBuilder<TMessage> ToGooglePubSub<TMessage>(
		this IMessageTypeMappingBuilder<TMessage> builder,
		Action<IGooglePubSubMappingContext> configure)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is MessageTypeMappingBuilder<TMessage> concrete)
		{
			concrete.Configuration.GooglePubSubConfiguration = configure;
		}

		return builder;
	}

	/// <summary>
	/// Configures gRPC-specific mapping for this message type.
	/// </summary>
	/// <typeparam name="TMessage">The message type being configured.</typeparam>
	/// <param name="builder">The message type mapping builder.</param>
	/// <param name="configure">Action to configure the gRPC context.</param>
	/// <returns>This builder for fluent configuration.</returns>
	public static IMessageTypeMappingBuilder<TMessage> ToGrpc<TMessage>(
		this IMessageTypeMappingBuilder<TMessage> builder,
		Action<IGrpcMappingContext> configure)
		where TMessage : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		if (builder is MessageTypeMappingBuilder<TMessage> concrete)
		{
			concrete.Configuration.GrpcConfiguration = configure;
		}

		return builder;
	}
}
