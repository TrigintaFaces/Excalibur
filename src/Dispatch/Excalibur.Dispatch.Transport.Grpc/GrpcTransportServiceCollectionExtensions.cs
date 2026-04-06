// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Grpc;
using Excalibur.Dispatch.Transport.Grpc.DeadLetter;
using Excalibur.Dispatch.Transport.Grpc.Diagnostics;

using Grpc.Net.Client;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering gRPC transport with the service collection.
/// </summary>
public static class GrpcTransportServiceCollectionExtensions
{
	/// <summary>
	/// Adds the gRPC transport with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The options configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddGrpcTransport(
		this IServiceCollection services,
		Action<GrpcTransportOptions> configure)
		=> AddGrpcTransport(services, "default", configure);

	/// <summary>
	/// Adds the gRPC transport using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="GrpcTransportOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	public static IServiceCollection AddGrpcTransport(
		this IServiceCollection services,
		IConfiguration configuration)
		=> AddGrpcTransport(services, "default", configuration);

	/// <summary>
	/// Adds the gRPC transport with the specified name and configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name used as the keyed service key.</param>
	/// <param name="configure">The options configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddGrpcTransport(
		this IServiceCollection services,
		string name,
		Action<GrpcTransportOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<GrpcTransportOptions>()
			.Configure(configure)
			.ValidateOnStart();

		RegisterGrpcCore(services, name);

		return services;
	}

	/// <summary>
	/// Adds the gRPC transport with the specified name using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name used as the keyed service key.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="GrpcTransportOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
	/// </exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddGrpcTransport(
		this IServiceCollection services,
		string name,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<GrpcTransportOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		RegisterGrpcCore(services, name);

		return services;
	}

	/// <summary>
	/// Registers the core gRPC transport services shared by all overloads.
	/// </summary>
	private static void RegisterGrpcCore(IServiceCollection services, string name)
	{
		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>().Value;

			var channelOptions = new GrpcChannelOptions();
			if (options.MaxSendMessageSize.HasValue)
			{
				channelOptions.MaxSendMessageSize = options.MaxSendMessageSize.Value;
			}

			if (options.MaxReceiveMessageSize.HasValue)
			{
				channelOptions.MaxReceiveMessageSize = options.MaxReceiveMessageSize.Value;
			}

			return GrpcChannel.ForAddress(options.ServerAddress, channelOptions);
		});

		services.AddKeyedSingleton<ITransportSender>(name, (sp, _) =>
		{
			var channel = sp.GetRequiredService<GrpcChannel>();
			var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>();
			var logger = sp.GetRequiredService<ILogger<GrpcTransportSender>>();
			return new GrpcTransportSender(channel, options, logger);
		});

		services.AddKeyedSingleton<ITransportReceiver>(name, (sp, _) =>
		{
			var channel = sp.GetRequiredService<GrpcChannel>();
			var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>();
			var logger = sp.GetRequiredService<ILogger<GrpcTransportReceiver>>();
			return new GrpcTransportReceiver(channel, options, logger);
		});

		services.AddKeyedSingleton<ITransportSubscriber>(name, (sp, _) =>
		{
			var channel = sp.GetRequiredService<GrpcChannel>();
			var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>();
			var logger = sp.GetRequiredService<ILogger<GrpcTransportSubscriber>>();
			return new GrpcTransportSubscriber(channel, options, logger);
		});

		// Register in-memory DLQ manager for gRPC transport (gRPC has no native DLQ)
		services.AddKeyedSingleton<IDeadLetterQueueManager>(name, (sp, _) =>
			new GrpcDeadLetterQueueManager(
				sp.GetRequiredService<ILogger<GrpcDeadLetterQueueManager>>()));

		// Register IValidateOptions for cross-property validation
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<GrpcTransportOptions>, GrpcTransportOptionsValidator>());

		// Register health check
		services.TryAddSingleton<GrpcTransportHealthCheck>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHealthCheck, GrpcTransportHealthCheck>());

		// Register transport adapter (bridges gRPC to dispatch pipeline)
		services.TryAddSingleton<GrpcTransportAdapter>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ITransportAdapter, GrpcTransportAdapter>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ITransportHealthChecker, GrpcTransportAdapter>());
	}
}
