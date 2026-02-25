// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Amazon.SQS;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS SQS transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for AWS SQS transport configuration.
/// </para>
/// <para>
/// Use <see cref="AddAwsSqsTransport(IServiceCollection, string, Action{IAwsSqsTransportBuilder})"/>
/// to register a named AWS SQS transport with full fluent configuration support.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsTransport("orders", sqs =>
/// {
///     sqs.UseRegion("us-east-1")
///        .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
///        .ConfigureFifo(fifo => fifo.ContentBasedDeduplication(true))
///        .ConfigureBatch(batch => batch.SendBatchSize(10))
///        .MapQueue&lt;OrderCreated&gt;("https://sqs.us-east-1.amazonaws.com/123/orders");
/// });
/// </code>
/// </example>
public static class AwsSqsTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "aws-sqs";

	/// <summary>
	/// Adds an AWS SQS transport with the specified name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name for multi-transport routing.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary entry point for AWS SQS transport configuration.
	/// It provides access to all fluent builder APIs for queue, FIFO, batch, and SNS configuration.
	/// </para>
	/// <para>
	/// Named transports support multi-transport routing scenarios where different message
	/// types are routed to different SQS transports.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for multi-transport scenarios
	/// services.AddAwsSqsTransport("orders", sqs =>
	/// {
	///     sqs.UseRegion("us-east-1")
	///        .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
	///        .MapQueue&lt;OrderCreated&gt;("https://sqs.us-east-1.amazonaws.com/123/orders");
	/// });
	///
	/// services.AddAwsSqsTransport("payments", sqs =>
	/// {
	///     sqs.UseRegion("us-west-2")
	///        .MapQueue&lt;PaymentReceived&gt;("https://sqs.us-west-2.amazonaws.com/123/payments");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAwsSqsTransport(
		this IServiceCollection services,
		string name,
		Action<IAwsSqsTransportBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		// Create and configure options via builder
		var adapterOptions = new AwsSqsTransportAdapterOptions { Name = name };
		var builder = new AwsSqsTransportBuilder(adapterOptions);
		configure(builder);

		// Register core AWS SQS services
		RegisterAwsSqsServices(services);

		// Register the transport adapter with the transport factory
		RegisterTransportAdapter(services, name, adapterOptions);

		// Register ITransportSubscriber with telemetry decorator
		RegisterSubscriber(services, name, adapterOptions);

		return services;
	}

	/// <summary>
	/// Adds an AWS SQS transport with the default name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "aws-sqs".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddAwsSqsTransport(sqs =>
	/// {
	///     sqs.UseRegion("us-east-1")
	///        .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)));
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddAwsSqsTransport(
		this IServiceCollection services,
		Action<IAwsSqsTransportBuilder> configure)
	{
		return services.AddAwsSqsTransport(DefaultTransportName, configure);
	}

	/// <summary>
	/// Registers the core AWS SQS services with the service collection.
	/// </summary>
	private static void RegisterAwsSqsServices(IServiceCollection services)
	{
		// Register AWS SQS client
		services.TryAddSingleton<IAmazonSQS>(static _ => new AmazonSQSClient());

		// Register SQS message bus
		services.TryAddSingleton<AwsSqsMessageBus>();

		// Register SQS channel receiver
		services.TryAddSingleton<AwsSqsChannelReceiver>();
	}

	/// <summary>
	/// Registers the transport adapter with the transport factory.
	/// </summary>
	private static void RegisterTransportAdapter(
		IServiceCollection services,
		string name,
		AwsSqsTransportAdapterOptions adapterOptions)
	{
		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsSqsTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AwsSqsMessageBus>();
			return new AwsSqsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<AwsSqsTransportAdapter>>();
			var messageBus = sp.GetRequiredService<AwsSqsMessageBus>();
			return new AwsSqsTransportAdapter(logger, messageBus, sp, adapterOptions);
		});

		// Register factory in TransportRegistry for lifecycle management
		// Uses keyed service resolution to support multi-transport scenarios
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			AwsSqsTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<AwsSqsTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();
	}

	/// <summary>
	/// Registers a keyed <see cref="ITransportSubscriber"/> composed with telemetry.
	/// </summary>
	private static void RegisterSubscriber(
		IServiceCollection services,
		string name,
		AwsSqsTransportAdapterOptions adapterOptions)
	{
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var sqsClient = sp.GetRequiredService<IAmazonSQS>();
			var logger = sp.GetRequiredService<ILogger<SqsTransportSubscriber>>();
			var queueUrl = adapterOptions.HasQueueMappings
				? adapterOptions.QueueMappings.Values.First()
				: name;
			var nativeSubscriber = new SqsTransportSubscriber(sqsClient, name, queueUrl, logger);

			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(TransportTelemetryConstants.MeterName(name)) ?? new Meter(TransportTelemetryConstants.MeterName(name));
			var activitySource = new ActivitySource(TransportTelemetryConstants.ActivitySourceName(name));

			return new TransportSubscriberBuilder(nativeSubscriber)
				.UseTelemetry(name, meter, activitySource)
				.Build();
		});
	}
}
