// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure;
using Azure.Identity;
using Azure.Messaging.EventGrid;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure Event Grid transport with the service collection.
/// </summary>
public static class EventGridTransportServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Azure Event Grid transport sender with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The options configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Azure Event Grid is a push-only service. This method registers an <see cref="ITransportSender"/>
	/// for publishing events. For receiving events, use Azure Functions Event Grid triggers or webhook subscriptions.
	/// </para>
	/// <para>
	/// Supports both access key and managed identity authentication. When <see cref="EventGridTransportOptions.AccessKey"/>
	/// is set, key-based authentication is used. Otherwise, <see cref="DefaultAzureCredential"/> is used.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Using access key
	/// services.AddEventGridTransport(options =>
	/// {
	///     options.TopicEndpoint = "https://mytopic.westus2-1.eventgrid.azure.net/api/events";
	///     options.AccessKey = "my-access-key";
	/// });
	///
	/// // Using managed identity
	/// services.AddEventGridTransport(options =>
	/// {
	///     options.TopicEndpoint = "https://mytopic.westus2-1.eventgrid.azure.net/api/events";
	///     options.UseManagedIdentity = true;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddEventGridTransport(
		this IServiceCollection services,
		Action<EventGridTransportOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<EventGridTransportOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		RegisterEventGridCore(services);

		return services;
	}

	/// <summary>
	/// Adds the Azure Event Grid transport sender using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="EventGridTransportOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	public static IServiceCollection AddEventGridTransport(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<EventGridTransportOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		RegisterEventGridCore(services);

		return services;
	}

	/// <summary>
	/// Registers the core Event Grid services shared by all overloads.
	/// </summary>
	private static void RegisterEventGridCore(IServiceCollection services)
	{
		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<EventGridTransportOptions>>().Value;
			var endpoint = new Uri(options.TopicEndpoint);

			if (!string.IsNullOrEmpty(options.AccessKey))
			{
				return new EventGridPublisherClient(endpoint, new AzureKeyCredential(options.AccessKey));
			}

			return new EventGridPublisherClient(endpoint, new DefaultAzureCredential());
		});

		services.AddKeyedSingleton<ITransportSender>("eventgrid", (sp, _) =>
		{
			var client = sp.GetRequiredService<EventGridPublisherClient>();
			var options = sp.GetRequiredService<IOptions<EventGridTransportOptions>>();
			var logger = sp.GetRequiredService<ILogger<EventGridTransportSender>>();
			var nativeSender = new EventGridTransportSender(client, options, logger);

			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(TransportTelemetryConstants.MeterName("eventgrid"))
				?? new Meter(TransportTelemetryConstants.MeterName("eventgrid"));
			var activitySource = new ActivitySource(TransportTelemetryConstants.ActivitySourceName("eventgrid"));

			return new TransportSenderBuilder(nativeSender)
				.UseTelemetry("eventgrid", meter, activitySource)
				.Build();
		});
	}
}
