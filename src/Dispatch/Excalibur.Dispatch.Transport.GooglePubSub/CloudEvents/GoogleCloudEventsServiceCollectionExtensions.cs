// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for Google Cloud CloudEvents integration.
/// </summary>
/// <remarks>
/// Provides DoD-compliant CloudEvents support for Google Cloud services including Pub/Sub with full envelope property preservation
/// across structured and binary modes.
/// </remarks>
public static class GoogleCloudEventsServiceCollectionExtensions
{
	/// <summary>
	/// Adds CloudEvents support to Google Cloud services with full envelope integrity preservation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Registers Google Cloud CloudEvent adapters with support for:
	/// - Structured mode (application/cloudevents+json)
	/// - Binary mode (CE attributes in Google Cloud message attributes)
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
		_ = services.AddOptions<GooglePubSubCloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}

		services.TryAddSingleton<ICloudEventEnvelopeConverter, CloudEventEnvelopeConverter>();
		services.TryAddSingleton<IEnvelopeCloudEventBridge, EnvelopeCloudEventBridge>();

		services.TryAddSingleton(static provider => provider.GetRequiredService<IOptions<CloudEventOptions>>().Value);
		services.TryAddSingleton(static provider =>
			provider.GetRequiredService<IOptions<GooglePubSubCloudEventOptions>>().Value);

		services.TryAddSingleton<ICloudEventMapper<PubsubMessage>, GooglePubSubCloudEventAdapter>();

		return services;
	}

	/// <summary>
	/// Adds CloudEvents support specifically for Google Pub/Sub with enhanced configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configurePubSub"> Action to configure Pub/Sub-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection UseCloudEventsForPubSub(
		this IServiceCollection services,
		Action<GooglePubSubCloudEventOptions>? configurePubSub = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.UseCloudEvents(configureGeneral);

		if (configurePubSub is not null)
		{
			_ = services.Configure(configurePubSub);
		}

		return services;
	}

	/// <summary>
	/// Adds CloudEvents validation with Google Cloud-specific envelope integrity checks.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="enableDoDCompliance"> Whether to enable DoD-specific envelope validation. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGoogleCloudEventValidation(
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
