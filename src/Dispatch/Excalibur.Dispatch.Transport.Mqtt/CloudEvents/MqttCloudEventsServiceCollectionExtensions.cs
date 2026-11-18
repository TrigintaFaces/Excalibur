// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using MQTTnet;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for MQTT CloudEvents integration.
/// </summary>
/// <remarks>
/// Mirrors <c>KafkaCloudEventsServiceCollectionExtensions</c>/<c>RabbitMqCloudEventsServiceCollectionExtensions</c>
/// -- the same two-method shape (general bridge registration, then transport-specific mapper registration).
/// </remarks>
public static class MqttCloudEventsServiceCollectionExtensions
{
	/// <summary>
	/// Adds CloudEvents support to MQTT services with full envelope integrity preservation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Registers the MQTT CloudEvent adapter with support for:
	/// - Structured mode (<c>application/cloudevents+json</c> payload)
	/// - Binary mode (CE attributes as MQTT v5 user properties)
	/// - Envelope property preservation (MessageId, CorrelationId, TenantId, UserId, TraceId, etc.)
	/// - Round-trip conversion with no attribute loss.
	/// </remarks>
	public static IServiceCollection UseCloudEvents(
		this IServiceCollection services,
		Action<CloudEventOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<CloudEventOptions>()
			.ValidateOnStart();
		_ = services.AddCloudEventOptionsValidation();
		_ = services.AddCloudEventSerializerGuard();
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
	/// Adds CloudEvents support specifically for MQTT with enhanced configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureMqtt"> Action to configure MQTT-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// The MQTT sender uses the registered <see cref="ICloudEventEncoder{TOutbound}"/> for messages
	/// carrying a CloudEvent in their <c>cloudevent</c> property. The encoder honors
	/// <see cref="CloudEventOptions.DefaultMode"/> and <see cref="MqttCloudEventOptions.Retain"/>;
	/// messages without a CloudEvent retain their ordinary payload and properties.
	/// </para>
	/// <para>
	/// Payload serialization is selected by the core <c>AddCloudEvents</c> registration. For trimming
	/// and ahead-of-time compilation, use its resolver overload with a source-generated context covering
	/// the types carried in <see cref="CloudNative.CloudEvents.CloudEvent.Data"/>. The bundled encoder
	/// uses that configured <see cref="CloudEventSerializer"/> rather than selecting reflection itself.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCloudEventsForMqtt(
		this IServiceCollection services,
		Action<MqttCloudEventOptions>? configureMqtt = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.UseCloudEvents(configureGeneral);

		_ = services.AddOptions<MqttCloudEventOptions>()
			.ValidateOnStart();
		if (configureMqtt is not null)
		{
			_ = services.Configure(configureMqtt);
		}

		services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<MqttCloudEventOptions>>().Value);

		services.AddCloudEventEncoder<MqttApplicationMessage, MqttCloudEventAdapter>();

		return services;
	}
}
