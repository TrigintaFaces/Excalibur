// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.EventBridge.Model;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for AWS CloudEvents integration.
/// </summary>
/// <remarks>
/// Provides DoD-compliant CloudEvents support for AWS services including SQS, SNS, and EventBridge with full envelope property preservation
/// across structured and binary modes.
/// </remarks>
public static class AwsCloudEventsServiceCollectionExtensions
{
	/// <summary>
	/// Adds CloudEvents support to AWS SQS services with full envelope integrity preservation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Registers AWS SQS CloudEvent adapter with support for:
	/// - Structured mode (application/cloudevents+json)
	/// - Binary mode (CE attributes in SQS message attributes)
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
		_ = services.AddOptions<AwsSqsCloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddOptions<AwsSnsCloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		_ = services.AddOptions<AwsEventBridgeCloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		if (configureOptions is not null)
		{
			_ = services.Configure(configureOptions);
		}

		services.TryAddSingleton<ICloudEventEnvelopeConverter, CloudEventEnvelopeConverter>();
		services.TryAddSingleton<IEnvelopeCloudEventBridge, EnvelopeCloudEventBridge>();

		services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<CloudEventOptions>>().Value);

		services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<AwsSqsCloudEventOptions>>().Value);
		services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<AwsSnsCloudEventOptions>>().Value);
		services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<AwsEventBridgeCloudEventOptions>>().Value);

		services.TryAddSingleton<ICloudEventMapper<SendMessageRequest>, AwsSqsCloudEventAdapter>();
		services.TryAddSingleton<ICloudEventMapper<PublishRequest>, AwsSnsCloudEventAdapter>();
		services.TryAddSingleton<ICloudEventMapper<PutEventsRequestEntry>, AwsEventBridgeCloudEventAdapter>();

		return services;
	}

	/// <summary>
	/// Adds CloudEvents support specifically for AWS SQS with enhanced configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureSqs"> Action to configure SQS-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection UseCloudEventsForSqs(
		this IServiceCollection services,
		Action<AwsSqsCloudEventOptions>? configureSqs = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure general CloudEvents
		_ = services.UseCloudEvents(configureGeneral);

		_ = services.AddOptions<AwsSqsCloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureSqs is not null)
		{
			_ = services.Configure(configureSqs);
		}

		return services;
	}

	/// <summary>
	/// Adds CloudEvents support specifically for AWS SNS with enhanced configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureSns"> Action to configure SNS-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection UseCloudEventsForSns(
		this IServiceCollection services,
		Action<AwsSnsCloudEventOptions>? configureSns = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure general CloudEvents
		_ = services.UseCloudEvents(configureGeneral);

		_ = services.AddOptions<AwsSnsCloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureSns is not null)
		{
			_ = services.Configure(configureSns);
		}

		return services;
	}

	/// <summary>
	/// Adds CloudEvents support specifically for AWS EventBridge with enhanced configuration options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureEventBridge"> Action to configure EventBridge-specific CloudEvent options. </param>
	/// <param name="configureGeneral"> Optional action to configure general CloudEvent options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection UseCloudEventsForEventBridge(
		this IServiceCollection services,
		Action<AwsEventBridgeCloudEventOptions>? configureEventBridge = null,
		Action<CloudEventOptions>? configureGeneral = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure general CloudEvents
		_ = services.UseCloudEvents(configureGeneral);

		_ = services.AddOptions<AwsEventBridgeCloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureEventBridge is not null)
		{
			_ = services.Configure(configureEventBridge);
		}

		return services;
	}

	/// <summary>
	/// Adds CloudEvents validation with AWS-specific envelope integrity checks.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="enableDoDCompliance"> Whether to enable DoD-specific envelope validation. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsCloudEventValidation(
		this IServiceCollection services,
		bool enableDoDCompliance = true)
	{
		ArgumentNullException.ThrowIfNull(services);

		if (enableDoDCompliance)
		{
			_ = services.Configure<CloudEventOptions>(static options => options.CustomValidator = (cloudEvent, cancellationToken) =>
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

	/// <summary>
	/// Adds CloudEvent transformation specifically for AWS envelope enrichment.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="transformer"> Custom transformer for AWS-specific CloudEvent processing. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAwsCloudEventTransformation(
		this IServiceCollection services,
		Func<CloudEvent, IDispatchEvent, IMessageContext, CancellationToken, Task> transformer)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(transformer);

		_ = services.Configure<CloudEventOptions>(options =>
		{
			var existingTransformer = options.OutgoingTransformer;
			options.OutgoingTransformer = async (ce, evt, ctx, ct) =>
			{
				// Call existing transformer first
				if (existingTransformer != null)
				{
					await existingTransformer(ce, evt, ctx, ct).ConfigureAwait(false);
				}

				// Apply AWS-specific transformation
				await transformer(ce, evt, ctx, ct).ConfigureAwait(false);
			};
		});

		return services;
	}
}
