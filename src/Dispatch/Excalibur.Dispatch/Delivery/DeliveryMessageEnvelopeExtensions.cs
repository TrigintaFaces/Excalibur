// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.ZeroAlloc;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Extension methods for configuring struct-based message envelopes.
/// </summary>
public static class DeliveryMessageEnvelopeExtensions
{
	/// <summary>
	/// Configures the dispatch system to use struct-based message envelopes.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional configuration for envelope options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddStructBasedMessageEnvelopes(
		this IServiceCollection services,
		Action<MessageEnvelopeOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var options = new MessageEnvelopeOptions();
		configureOptions?.Invoke(options);

		// Register envelope pool
		_ = services.AddSingleton<IMessageEnvelopePool>(serviceProvider =>
		{
			var messagePool = serviceProvider.GetRequiredService<IMessagePool>();
			return new MessageEnvelopePool(
				messagePool,
				new MessageEnvelopePoolOptions
				{
					ThreadLocalCacheSize = options.ThreadLocalCacheSize,
					EnableTelemetry = options.EnableTelemetry,
				});
		});

		// Register optimized dispatcher
		_ = services.RemoveAll<IDispatcher>();
		_ = services.AddSingleton<IDispatcher, Dispatcher>();

		// Register context factory
		services.TryAddSingleton<IMessageContextPool>(static sp => new MessageContextPool(sp));
		services.TryAddSingleton<IMessageContextFactory>(static sp =>
			new PooledMessageContextFactory(sp.GetRequiredService<IMessageContextPool>()));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use struct-based message envelopes.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configureOptions"> Optional configuration for envelope options. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder WithStructBasedEnvelopes(
		this IDispatchBuilder builder,
		Action<MessageEnvelopeOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddStructBasedMessageEnvelopes(configureOptions);
		return builder;
	}

	/// <summary>
	/// Creates a message envelope from a message with default metadata.
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message. </typeparam>
	/// <param name="message"> The message to wrap. </param>
	/// <returns> A message envelope. </returns>
	public static MessageEnvelope<TMessage> ToEnvelope<TMessage>(this TMessage message)
		where TMessage : IDispatchMessage =>
		new(message, MessageMetadata.Default.ToRecordMetadata());

	/// <summary>
	/// Creates a message envelope from a message with custom metadata.
	/// </summary>
	/// <typeparam name="TMessage"> The type of the message. </typeparam>
	/// <param name="message"> The message to wrap. </param>
	/// <param name="configureMetadata"> Action to configure metadata. </param>
	/// <returns> A message envelope. </returns>
	public static MessageEnvelope<TMessage> ToEnvelope<TMessage>(
		this TMessage message,
		Func<MessageMetadataBuilder, Messaging.MessageMetadata> configureMetadata)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(configureMetadata);

		var builder = new MessageMetadataBuilder();
		var metadata = configureMetadata(builder);
		return new MessageEnvelope<TMessage>(message, metadata);
	}
}
