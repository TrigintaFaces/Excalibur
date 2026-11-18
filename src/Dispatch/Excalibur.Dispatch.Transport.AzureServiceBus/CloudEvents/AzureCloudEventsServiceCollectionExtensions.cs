// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.EventHubs;
using Azure.Messaging.ServiceBus;

using CloudNative.CloudEvents;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for Azure CloudEvents integration.
/// </summary>
/// <remarks>
/// Provides DoD-compliant CloudEvents support for Azure services including Service Bus and Event Hubs with full envelope property
/// preservation across structured and binary modes.
/// </remarks>
public static class AzureCloudEventsServiceCollectionExtensions
{
	/// <summary>
	/// Adds CloudEvents support to Azure services with full envelope integrity preservation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Registers Azure CloudEvent adapters with support for:
	/// - Structured mode (application/cloudevents+json)
	/// - Binary mode (CE attributes in Azure message properties)
	/// - DoD envelope property preservation (MessageId, CorrelationId, TenantId, UserId, TraceId, etc.)
	/// - Round-trip conversion with no attribute loss.
	/// <para>
	/// Payload serialization is selected separately by the core <c>AddCloudEvents</c> registration.
	/// For trimming and ahead-of-time compilation, use its resolver overload with a source-generated
	/// context covering the published payload types. The bundled encoder uses the configured
	/// <see cref="CloudEventSerializer"/>; a custom encoder is not required to select metadata serialization.
	/// The parameterless core overload remains the annotated reflection-based choice.
	/// Validate the selected transport dependencies and custom converters separately for Native AOT.
	/// </para>
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
		_ = services.AddOptions<AzureServiceBusCloudEventOptions>()
			.ValidateOnStart();
		_ = services.AddOptions<AzureEventHubsCloudEventOptions>()
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AzureServiceBusCloudEventOptions>, AzureServiceBusCloudEventOptionsValidator>());

		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}

		// Register CloudEvent envelope converter
		services.TryAddSingleton<ICloudEventEnvelopeConverter, CloudEventEnvelopeConverter>();
		services.TryAddSingleton<IEnvelopeCloudEventBridge, EnvelopeCloudEventBridge>();

		services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<CloudEventOptions>>().Value);

		// Register Azure-specific CloudEvent adapters
		services.AddCloudEventEncoder<ServiceBusMessage, AzureServiceBusCloudEventAdapter>();
		services.AddCloudEventEncoder<EventData, AzureEventHubsCloudEventAdapter>();
		services.TryAddSingleton<IAzureEventHubsCloudEventAdapter, AzureEventHubsCloudEventAdapter>();

		return services;
	}

	/// <summary>
	/// Adds CloudEvents support specifically for Azure Service Bus with enhanced this.configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureServiceBus"> Action to configure Service Bus-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Payload serialization is selected separately by the core <c>AddCloudEvents</c> registration.
	/// For trimming and ahead-of-time compilation, use its resolver overload with a source-generated
	/// context covering the published payload types. The bundled encoder uses the configured
	/// <see cref="CloudEventSerializer"/>; a custom encoder is not required to select metadata serialization.
	/// The parameterless core overload remains the annotated reflection-based choice.
	/// Validate the selected transport dependencies and custom converters separately for Native AOT.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCloudEventsForServiceBus(
		this IServiceCollection services,
		Action<AzureServiceBusCloudEventOptions>? configureServiceBus = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure general CloudEvents
		_ = services.UseCloudEvents(configureGeneral);

		_ = services.AddOptions<AzureServiceBusCloudEventOptions>()
			.ValidateOnStart();
		if (configureServiceBus is not null)
		{
			_ = services.Configure(configureServiceBus);
		}

		services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<AzureServiceBusCloudEventOptions>>().Value);

		return services;
	}

	/// <summary>
	/// Adds CloudEvents support specifically for Azure Event Hubs with enhanced configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureEventHubs"> Action to configure Event Hubs-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// Payload serialization is selected separately by the core <c>AddCloudEvents</c> registration.
	/// For trimming and ahead-of-time compilation, use its resolver overload with a source-generated
	/// context covering the published payload types. The bundled encoder uses the configured
	/// <see cref="CloudEventSerializer"/>; a custom encoder is not required to select metadata serialization.
	/// The parameterless core overload remains the annotated reflection-based choice.
	/// Validate the selected transport dependencies and custom converters separately for Native AOT.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCloudEventsForEventHubs(
		this IServiceCollection services,
		Action<AzureEventHubsCloudEventOptions>? configureEventHubs = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure general CloudEvents
		_ = services.UseCloudEvents(configureGeneral);

		_ = services.AddOptions<AzureEventHubsCloudEventOptions>()
			.ValidateOnStart();
		if (configureEventHubs is not null)
		{
			_ = services.Configure(configureEventHubs);
		}

		return services;
	}

	/// <summary>
	/// Adds CloudEvents validation with Azure-specific envelope integrity checks.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="enableDoDCompliance"> Whether to enable DoD-specific envelope validation. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAzureCloudEventValidation(
		this IServiceCollection services,
		bool enableDoDCompliance = true)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (enableDoDCompliance)
		{
			_ = services.Configure<CloudEventOptions>(static options => options.Schema.CustomValidator =
				static async (cloudEvent, cancellationToken) =>
				{
					// Validate DoD-required envelope properties
					var hasCorrelationId = cloudEvent.GetAttribute("correlationid") != null;
					var hasUserId = cloudEvent.GetAttribute("userid") != null;
					var hasTraceParent = cloudEvent.GetAttribute("traceparent") != null;
					var hasTimestamp = cloudEvent.Time.HasValue;

					// DoD compliance requires correlationId, userId, and traceParent — all mandatory.
					return await Task.FromResult(hasCorrelationId && hasUserId && hasTraceParent).ConfigureAwait(false);
				});
		}

		return services;
	}

	/// <summary>
	/// Adds CloudEvent transformation specifically for Azure envelope enrichment.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="transformer"> Custom transformer for Azure-specific CloudEvent processing. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAzureCloudEventTransformation(
		this IServiceCollection services,
		Func<CloudEvent, IDispatchEvent, IMessageContext, CancellationToken, Task> transformer)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(transformer);

		_ = services.Configure<CloudEventOptions>(options =>
		{
			var existingTransformer = options.Schema.OutgoingTransformer;
			options.Schema.OutgoingTransformer = async (ce, evt, ctx, ct) =>
			{
				// Call existing transformer first
				if (existingTransformer != null)
				{
					await existingTransformer(ce, evt, ctx, ct).ConfigureAwait(false);
				}

				// Apply Azure-specific transformation
				await transformer(ce, evt, ctx, ct).ConfigureAwait(false);
			};
		});

		return services;
	}
}
