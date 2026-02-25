// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.DependencyInjection.Extensions;
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
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<GrpcTransportOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

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

		services.TryAddSingleton<ITransportSender>(sp =>
		{
			var channel = sp.GetRequiredService<GrpcChannel>();
			var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>();
			var logger = sp.GetRequiredService<ILogger<GrpcTransportSender>>();
			return new GrpcTransportSender(channel, options, logger);
		});

		services.TryAddSingleton<ITransportReceiver>(sp =>
		{
			var channel = sp.GetRequiredService<GrpcChannel>();
			var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>();
			var logger = sp.GetRequiredService<ILogger<GrpcTransportReceiver>>();
			return new GrpcTransportReceiver(channel, options, logger);
		});

		services.TryAddSingleton<ITransportSubscriber>(sp =>
		{
			var channel = sp.GetRequiredService<GrpcChannel>();
			var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>();
			var logger = sp.GetRequiredService<ILogger<GrpcTransportSubscriber>>();
			return new GrpcTransportSubscriber(channel, options, logger);
		});

		return services;
	}
}
