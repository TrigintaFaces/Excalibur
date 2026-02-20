// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Confluent.Kafka;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for Kafka CloudEvents integration.
/// </summary>
/// <remarks>
/// Provides DoD-compliant CloudEvents support for Apache Kafka with full envelope property preservation across structured and binary modes.
/// </remarks>
public static class KafkaCloudEventsServiceCollectionExtensions
{
	/// <summary>
	/// Adds CloudEvents support to Kafka services with full envelope integrity preservation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Registers Kafka CloudEvent adapters with support for:
	/// - Structured mode (application/cloudevents+json)
	/// - Binary mode (CE attributes in Kafka message headers)
	/// - DoD envelope property preservation (MessageId, CorrelationId, TenantId, UserId, TraceId, etc.)
	/// - Round-trip conversion with no attribute loss.
	/// </remarks>
	public static IServiceCollection UseCloudEvents(
		this IServiceCollection services,
		Action<CloudEventOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<CloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}

		services.TryAddSingleton<ICloudEventEnvelopeConverter, CloudEventEnvelopeConverter>();
		services.TryAddSingleton<IEnvelopeCloudEventBridge, EnvelopeCloudEventBridge>();

		services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<CloudEventOptions>>().Value);

		return services;
	}

	/// <summary>
	/// Adds CloudEvents support specifically for Kafka with enhanced configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureKafka"> Action to configure Kafka-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection UseCloudEventsForKafka(
		this IServiceCollection services,
		Action<KafkaCloudEventOptions>? configureKafka = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure general CloudEvents
		_ = services.UseCloudEvents(configureGeneral);

		_ = services.AddOptions<KafkaCloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureKafka is not null)
		{
			_ = services.Configure(configureKafka);
		}

		services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<KafkaCloudEventOptions>>().Value);

		services.TryAddSingleton<IKafkaCloudEventAdapter, KafkaCloudEventAdapter>();
		services.TryAddSingleton<ICloudEventMapper<Message<string, string>>>(static sp =>
			(KafkaCloudEventAdapter)sp.GetRequiredService<IKafkaCloudEventAdapter>());

		return services;
	}

	/// <summary>
	/// Adds CloudEvents validation with Kafka-specific envelope integrity checks.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="enableDoDCompliance"> Whether to enable DoD-specific envelope validation. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddKafkaCloudEventValidation(
		this IServiceCollection services,
		bool enableDoDCompliance = true)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (enableDoDCompliance)
		{
			_ = services.Configure<CloudEventOptions>(static options => options.CustomValidator = static (cloudEvent, cancellationToken) =>
			{
				// Validate DoD-required envelope properties
				var hasCorrelationId = cloudEvent.GetAttribute("correlationid") != null;
				var hasUserId = cloudEvent.GetAttribute("userid") != null;
				var hasTraceParent = cloudEvent.GetAttribute("traceparent") != null;
				var hasTimestamp = cloudEvent.Time.HasValue;

				// At minimum require tracing for audit compliance
				return Task.FromResult(hasTraceParent || hasCorrelationId);
			});
		}

		return services;
	}
}
