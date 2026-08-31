// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for RabbitMQ CloudEvents integration.
/// </summary>
/// <remarks>
/// Provides DoD-compliant CloudEvents support for RabbitMQ with full envelope property preservation across structured and binary modes.
/// </remarks>
public static class RabbitMqCloudEventsServiceCollectionExtensions
{
	/// <summary>
	/// Adds CloudEvents support to RabbitMQ services with full envelope integrity preservation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Registers RabbitMQ CloudEvent adapters with support for:
	/// - Structured mode (application/cloudevents+json)
	/// - Binary mode (CE attributes in RabbitMQ message headers)
	/// - DoD envelope property preservation (MessageId, CorrelationId, TenantId, UserId, TraceId, etc.)
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
	/// Adds CloudEvents support specifically for RabbitMQ with enhanced configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureRabbitMq"> Action to configure RabbitMQ-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Trimming and ahead-of-time compilation: the CloudEvents mapper bundled with this transport
	/// serializes the message payload with reflection-based JSON, so a host that trims or compiles
	/// ahead of time warns at this call. Register your own <see cref="ICloudEventMapper{TTransportMessage}"/>
	/// backed by a source-generated serializer to compose without the requirement.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("The bundled CloudEvents mapper serializes the message payload through reflection-based JSON, so a trimmed host may lose types it needs. Register your own ICloudEventMapper over a source-generated serializer instead.")]
	[RequiresDynamicCode("The bundled CloudEvents mapper serializes the message payload through reflection-based JSON, which needs run-time code generation. Register your own ICloudEventMapper over a source-generated serializer instead.")]
	public static IServiceCollection AddCloudEventsForRabbitMq(
		this IServiceCollection services,
		Action<RabbitMqCloudEventOptions>? configureRabbitMq = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.UseCloudEvents(configureGeneral);

		_ = services.AddOptions<RabbitMqCloudEventOptions>()
			.ValidateOnStart();
		if (configureRabbitMq is not null)
		{
			_ = services.Configure(configureRabbitMq);
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RabbitMqCloudEventOptions>, RabbitMqCloudEventOptionsValidator>());
		services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<RabbitMqCloudEventOptions>>().Value);

		services.TryAddSingleton<IRabbitMqCloudEventAdapter, RabbitMqCloudEventAdapter>();
		services.AddCloudEventMapper<(IBasicProperties properties, ReadOnlyMemory<byte> body)>(static sp =>
			(RabbitMqCloudEventAdapter)sp.GetRequiredService<IRabbitMqCloudEventAdapter>());

		return services;
	}

	/// <summary>
	/// Adds CloudEvents validation with RabbitMQ-specific envelope integrity checks.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="enableDoDCompliance"> Whether to enable DoD-specific envelope validation. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddRabbitMqCloudEventValidation(
		this IServiceCollection services,
		bool enableDoDCompliance = true)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (enableDoDCompliance)
		{
			_ = services.Configure<CloudEventOptions>(static options => options.Schema.CustomValidator = static (cloudEvent, cancellationToken) =>
			{
				// Validate DoD-required envelope properties
				var hasCorrelationId = cloudEvent.GetAttribute("correlationid") != null;
				var hasUserId = cloudEvent.GetAttribute("userid") != null;
				var hasTraceParent = cloudEvent.GetAttribute("traceparent") != null;
				var hasTimestamp = cloudEvent.Time.HasValue;

				// DoD compliance requires correlationId, userId, and traceParent — all mandatory.
				return Task.FromResult(hasCorrelationId && hasUserId && hasTraceParent);
			});
		}

		return services;
	}
}
